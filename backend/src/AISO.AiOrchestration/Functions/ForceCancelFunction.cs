using System.Text.Json;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>Admin override: force-cancel an SO via SAP (bypasses ownership).</summary>
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
        "Admin-only: force cancel a sales order in SAP, bypassing ownership. Requires an override reason.";

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
              "description": "Mandatory override reason."
            }
          },
          "required": ["order_id", "reason"]
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
        if (string.IsNullOrWhiteSpace(reason))
            return FunctionResult.Fail("Missing required parameter: reason");

        try
        {
            var updated = await _sap.ForceCancelAsync(orderId, requestingSapUser, reason, ct);
            _logger.LogInformation(
                "ForceCancel: so={SoNumber} by={User} reason={Reason}",
                updated.SoNumber, requestingSapUser, reason);
            return FunctionResult.Ok(new
            {
                order_id = updated.SoNumber,
                action = "ForceCancelled",
                reason,
                message = $"Sales order {updated.SoNumber} was force-cancelled by Admin."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ForceCancel failed for {OrderId}", orderId);
            return FunctionResult.Fail($"Force cancel failed: {ex.Message}");
        }
    }
}
