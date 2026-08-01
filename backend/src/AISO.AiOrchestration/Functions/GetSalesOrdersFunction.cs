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
        "sales organization, date range, status, and ownership. " +
        "For 'my sales orders' / 'đơn của tôi' / 'đơn hàng của tôi', set ownedByMe=true " +
        "(filters OwnerSapUser to the requesting SAP user). " +
        "For 'recent orders' / 'show open orders' / 'all open orders' without 'my', leave ownedByMe unset.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "customerIdOrName": {
              "type": "string",
              "description": "Customer ID (exact eq on Customer, e.g. '1000') or partial customer name (contains on CustomerName, e.g. 'Philly Bikes')."
            },
            "salesOrg": {
              "type": "string",
              "enum": ["TV01", "FU24", "UE00", "UW00", "DN00", "DS00"],
              "description": "Sales organization code."
            },
            "fromDate": { "type": "string", "format": "date" },
            "toDate":   { "type": "string", "format": "date" },
            "status": {
              "type": "string",
              "enum": ["Open", "Blocked", "PartiallyDelivered", "Delivered", "Invoiced", "Cancelled"]
            },
            "ownedByMe": {
              "type": "boolean",
              "description": "When true, only return orders owned by the requesting SAP user (OwnerSapUser). Use for 'my orders' / 'của tôi'."
            },
            "top": { "type": "integer", "default": 10, "minimum": 1, "maximum": 50 }
          },
          "additionalProperties": false
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
    {
        var ownedByMe = GetBool(parameters, "ownedByMe")
                     ?? GetBool(parameters, "mine_only")
                     ?? GetBool(parameters, "owned_by_me");

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
            OwnerSapUser = ownedByMe == true
                ? requestingSapUser
                : GetString(parameters, "ownerSapUser") ?? GetString(parameters, "owner_sap_user"),
            Top = GetInt(parameters, "top") ?? 10
        };

        _logger.LogInformation(
            "Executing getSalesOrders: customer={Customer}, salesOrg={SalesOrg}, " +
            "from={FromDate}, to={ToDate}, status={Status}, owner={Owner}, top={Top}",
            query.CustomerIdOrName, query.SalesOrg, query.FromDate, query.ToDate,
            query.Status, query.OwnerSapUser, query.Top);

        var orders = await _sap.GetSalesOrdersAsync(query, ct);

        _logger.LogInformation(
            "getSalesOrders returned {Count} orders", orders.Count);

        string? chartUrl = null;
        if (orders.Any())
        {
            var statusCounts = orders.GroupBy(o => o.Status)
                                     .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                                     .ToList();

            var labels = string.Join(",", statusCounts.Select(x => $"'{x.Status}'"));
            var data = string.Join(",", statusCounts.Select(x => x.Count));

            var chartConfig = $$"""
            {
              type: 'doughnut',
              data: {
                labels: [{{labels}}],
                datasets: [{
                  data: [{{data}}]
                }]
              },
              options: {
                plugins: {
                  title: { display: true, text: 'Order Status Distribution' },
                  datalabels: { display: true, color: '#fff', font: { weight: 'bold' } }
                }
              }
            }
            """;

            chartConfig = System.Text.RegularExpressions.Regex.Replace(chartConfig, @"\s+", "");
            chartUrl = $"https://quickchart.io/chart?c={Uri.EscapeDataString(chartConfig)}";
        }

        var title = ownedByMe == true ? "My sales orders" : "Sales orders";
        var response = new GetSalesOrdersResponse(orders, chartUrl, title);

        return FunctionResult.Ok(response);
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    private static bool? GetBool(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
            return null;
        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(p.GetString(), out var b) => b,
            _ => null
        };
    }

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

public record GetSalesOrdersResponse(
    IReadOnlyList<SalesOrder> Orders,
    string? ChartUrl,
    string Title = "Sales orders");
