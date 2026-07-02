using System.Text.Json;
using AISO.Domain.Kpi;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

public sealed class GetOverdueOrdersFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly ILogger<GetOverdueOrdersFunction> _logger;

    public GetOverdueOrdersFunction(ISapClient sap, ILogger<GetOverdueOrdersFunction> logger)
    {
        _sap = sap;
        _logger = logger;
    }

    public string Name => "GetOverdueOrders";

    public string Description =>
        "Get sales orders that have exceeded their scheduled delivery date from SAP. " +
        "Trigger when user asks about late orders, overdue deliveries, delayed shipments, " +
        "or orders past due date. Returns orders sorted most-overdue first.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "customerIdOrName": {
              "type": "string",
              "description": "Customer ID or partial name filter."
            },
            "salesOrg": {
              "type": "string",
              "enum": ["UE00", "UW00", "DN00", "DS00"]
            },
            "daysPastDue": {
              "type": "integer",
              "minimum": 1,
              "description": "Minimum days past scheduled delivery date to include."
            },
            "top": { "type": "integer", "default": 20, "minimum": 1, "maximum": 50,
                     "description": "Max records returned, ordered most-overdue first." }
          },
          "additionalProperties": false
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
    {
        var query = new OverdueOrdersQuery
        {
            CustomerIdOrName = GetString(parameters, "customerIdOrName"),
            SalesOrg = GetString(parameters, "salesOrg"),
            DaysPastDue = GetInt(parameters, "daysPastDue"),
            Top = GetInt(parameters, "top") ?? 20
        };

        _logger.LogInformation(
            "Executing GetOverdueOrders: customer={Customer}, salesOrg={SalesOrg}, " +
            "daysPastDue={Days}, top={Top}",
            query.CustomerIdOrName, query.SalesOrg, query.DaysPastDue, query.Top);

        var orders = await _sap.GetOverdueOrdersAsync(query, ct);

        _logger.LogInformation("GetOverdueOrders returned {Count} overdue orders", orders.Count);

        return FunctionResult.Ok(new GetOverdueOrdersResponse(orders));
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static int? GetInt(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null;
}

public record GetOverdueOrdersResponse(IReadOnlyList<OverdueOrder> Orders);
