using System.Globalization;
using System.Text.Json;
using AISO.Domain.SalesOrders;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

public sealed class GetSalesOrdersFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly ILogger<GetSalesOrdersFunction> _logger;

    public GetSalesOrdersFunction(
        ISapClient sap,
        ILogger<GetSalesOrdersFunction> logger)
    {
        _sap = sap;
        _logger = logger;
    }

    public string Name => "GetSalesOrders";

    public string Description =>
        "Retrieve sales orders. Supports filtering by customer (ID or partial name), " +
        "sales organization (UE00/UW00/DN00/DS00), date range, and status. " +
        "Returns the most recent orders matching the filter.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "customerIdOrName": {
              "type": "string",
              "description": "Customer ID (e.g. '1000') or partial customer name (e.g. 'Philly')."
            },
            "salesOrg": {
              "type": "string",
              "enum": ["UE00", "UW00", "DN00", "DS00"],
              "description": "Sales organization code."
            },
            "fromDate": { "type": "string", "format": "date" },
            "toDate":   { "type": "string", "format": "date" },
            "status": {
              "type": "string",
              "enum": ["Open", "Blocked", "PartiallyDelivered", "Delivered", "Invoiced", "Cancelled"]
            },
            "top": { "type": "integer", "default": 10, "minimum": 1, "maximum": 50 }
          },
          "additionalProperties": false
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
    {
        // Support both BE param names and AI team param names
        var query = new SalesOrdersQuery
        {
            CustomerIdOrName = GetString(parameters, "customerIdOrName")
                            ?? GetString(parameters, "customer_id"),
            SalesOrg = GetString(parameters, "salesOrg"),
            FromDate = GetDate(parameters, "fromDate")
                    ?? GetDate(parameters, "date_from"),
            ToDate = GetDate(parameters, "toDate")
                  ?? GetDate(parameters, "date_to"),
            Status = GetEnum<SalesOrderStatus>(parameters, "status"),
            Top = GetInt(parameters, "top") ?? 10
        };

        _logger.LogInformation(
            "Executing getSalesOrders: customer={Customer}, salesOrg={SalesOrg}, " +
            "from={FromDate}, to={ToDate}, status={Status}, top={Top}",
            query.CustomerIdOrName, query.SalesOrg, query.FromDate, query.ToDate,
            query.Status, query.Top);

        var orders = await _sap.GetSalesOrdersAsync(query, ct);

        _logger.LogInformation(
            "getSalesOrders returned {Count} orders", orders.Count);

        // Generate a professional QuickChart.io URL for the FE
        string? chartUrl = null;
        if (orders.Any())
        {
            var statusCounts = orders.GroupBy(o => o.Status)
                                     .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                                     .ToList();

            var labels = string.Join(",", statusCounts.Select(x => $"'{x.Status}'"));
            var data = string.Join(",", statusCounts.Select(x => x.Count));

            // Build a proper JSON chart config and serialize it to ensure QuickChart accepts it.
            var chartObject = new
            {
                type = "doughnut",
                data = new
                {
                    labels = statusCounts.Select(x => x.Status).ToArray(),
                    datasets = new[]
                    {
                        new { data = statusCounts.Select(x => x.Count).ToArray() }
                    }
                },
                options = new
                {
                    plugins = new
                    {
                        title = new { display = true, text = "Order Status Distribution" }
                    }
                }
            };

            var chartJson = JsonSerializer.Serialize(chartObject);
            // Request PNG format and set dimensions to improve Teams rendering reliability.
            chartUrl = $"https://quickchart.io/chart?c={Uri.EscapeDataString(chartJson)}&format=png&width=600&height=400";
        }

        var response = new GetSalesOrdersResponse(orders, chartUrl);

        return FunctionResult.Ok(response);
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    private static int? GetInt(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number
            ? p.GetInt32()
            : null;

    private static DateOnly? GetDate(JsonElement el, string name)
    {
        var s = GetString(el, name);
        return s is not null
               && DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var d)
            ? d
            : null;
    }

    private static T? GetEnum<T>(JsonElement el, string name) where T : struct, Enum
    {
        var s = GetString(el, name);
        return s is not null && Enum.TryParse<T>(s, ignoreCase: true, out var v) ? v : null;
    }
}

public record GetSalesOrdersResponse(IReadOnlyList<SalesOrder> Orders, string? ChartUrl);

