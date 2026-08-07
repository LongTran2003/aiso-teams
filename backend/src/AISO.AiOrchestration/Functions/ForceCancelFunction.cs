using System.Text.Json;
using AISO.Domain.SalesOrders;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Admin override: validate then show confirm card (reason required).
/// SAP call happens on Adaptive Card <c>force_cancel_confirm</c>.
/// </summary>
public sealed class ForceCancelFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly ILogger<ForceCancelFunction> _logger;

    public ForceCancelFunction(ISapClient sap, ILogger<ForceCancelFunction> logger)
    {
        _sap = sap;
        _logger = logger;
    }

    public string Name => "ForceCancel";

    public string Description =>
        "Admin-only: prepare force cancel for a sales order (bypasses ownership). " +
        "Returns a confirmation card — does not cancel until the user confirms with a reason.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "order_id": {
              "type": "string",
              "description": "The unique sales order identifier."
            },
            "reason": {
              "type": "string",
              "description": "Optional draft override reason to prefill on the confirm card."
            }
          },
          "required": ["order_id"]
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
    {
        var orderId = parameters.TryGetProperty("order_id", out var p)
                      && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
        var reason = parameters.TryGetProperty("reason", out var r)
                     && r.ValueKind == JsonValueKind.String
            ? r.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(orderId))
            return FunctionResult.Fail("Missing required parameter: order_id");

        try
        {
            var existing = await _sap.GetSalesOrderByIdAsync(orderId, ct);
            if (existing is null)
                return FunctionResult.Fail($"Sales order {orderId} was not found in SAP.", "NOT_FOUND");

            if (SalesOrderWorkflow.BlocksReject(existing.Status))
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildBlockedMessage(existing.Status, "Force cancel"),
                    "VALIDATION");
            }

            _logger.LogInformation(
                "ForceCancel confirm step: so={SoNumber} by={User} (not submitted yet)",
                existing.SoNumber, requestingSapUser);

            return FunctionResult.Ok(new ConfirmForceCancelResponse(
                existing.SoNumber,
                string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ForceCancel prepare failed for {OrderId}", orderId);
            return FunctionResult.Fail($"Force cancel failed: {ex.Message}");
        }
    }
}

/// <summary>Payload telling the bot to show <c>confirm-force-cancel</c>.</summary>
public sealed record ConfirmForceCancelResponse(string SoNumber, string? Reason = null);
