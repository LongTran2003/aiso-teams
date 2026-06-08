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

    public string Name => "getSalesOrders";

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

    public async Task<FunctionResult> ExecuteAsync(
        JsonElement parameters, CancellationToken ct = default)
    {
        var query = new SalesOrdersQuery
        {
            CustomerIdOrName = GetString(parameters, "customerIdOrName"),
            SalesOrg         = GetString(parameters, "salesOrg"),
            FromDate         = GetDate(parameters, "fromDate"),
            ToDate           = GetDate(parameters, "toDate"),
            Status           = GetEnum<SalesOrderStatus>(parameters, "status"),
            Top              = GetInt(parameters, "top") ?? 10
        };

        _logger.LogInformation(
            "Executing getSalesOrders: customer={Customer}, salesOrg={SalesOrg}, " +
            "from={FromDate}, to={ToDate}, status={Status}, top={Top}",
            query.CustomerIdOrName, query.SalesOrg, query.FromDate, query.ToDate,
            query.Status, query.Top);

        var orders = await _sap.GetSalesOrdersAsync(query, ct);

        _logger.LogInformation(
            "getSalesOrders returned {Count} orders", orders.Count);

        return FunctionResult.Ok(orders);
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
