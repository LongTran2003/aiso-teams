using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Rejects a Sales Order in SAP with a reason code.
/// Maps to AI function schema <c>RejectOrder</c>.
/// Sprint 2-3: returns mock success. Sprint 4: calls SAP RAP action.
/// </summary>
public sealed class RejectOrderFunction : IFunction
{
    private readonly ILogger<RejectOrderFunction> _logger;

    public RejectOrderFunction(ILogger<RejectOrderFunction> logger)
    {
        _logger = logger;
    }

    public string Name => "RejectOrder";

    public string Description =>
        "Reject or cancel a sales order in the SAP ERP system with a reason code.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "order_id": {
              "type": "string",
              "description": "The unique sales order identifier (e.g. '0000005001')."
            },
            "reason_code": {
              "type": "string",
              "enum": ["PRICE_ISSUE", "OUT_OF_STOCK", "OTHER"],
              "description": "The reason for rejection."
            }
          },
          "required": ["order_id", "reason_code"]
        }
        """;

    public Task<FunctionResult> ExecuteAsync(
        JsonElement parameters, CancellationToken ct = default)
    {
        var orderId = parameters.TryGetProperty("order_id", out var p)
                      && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

        var reasonCode = parameters.TryGetProperty("reason_code", out var r)
                         && r.ValueKind == JsonValueKind.String
            ? r.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(orderId))
        {
            return Task.FromResult(FunctionResult.Fail("Missing required parameter: order_id"));
        }

        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            return Task.FromResult(FunctionResult.Fail("Missing required parameter: reason_code"));
        }

        _logger.LogInformation(
            "RejectOrder: orderId={OrderId}, reasonCode={ReasonCode}", orderId, reasonCode);

        // Sprint 2-3: mock success. Sprint 4: call SAP RAP action.
        var result = new
        {
            order_id = orderId,
            action = "Rejected",
            reason_code = reasonCode,
            message = $"Sales order {orderId} has been rejected (reason: {reasonCode})."
        };

        return Task.FromResult(FunctionResult.Ok(result));
    }
}
