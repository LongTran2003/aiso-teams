using System.Text.Json;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>Admin override: force-release an SO via SAP (bypasses ownership).</summary>
public sealed class ForceReleaseFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly ILogger<ForceReleaseFunction> _logger;

    public ForceReleaseFunction(ISapClient sap, ILogger<ForceReleaseFunction> logger)
    {
        _sap = sap;
        _logger = logger;
    }

    public string Name => "ForceRelease";

    public string Description =>
        "Admin-only: force release a sales order in SAP, bypassing ownership. Requires an override reason.";

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
            var updated = await _sap.ForceReleaseAsync(orderId, requestingSapUser, reason, ct);
            _logger.LogInformation(
                "ForceRelease: so={SoNumber} by={User} reason={Reason}",
                updated.SoNumber, requestingSapUser, reason);
            return FunctionResult.Ok(new
            {
                order_id = updated.SoNumber,
                action = "ForceReleased",
                reason,
                message = $"Sales order {updated.SoNumber} was force-released by Admin."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ForceRelease failed for {OrderId}", orderId);
            return FunctionResult.Fail($"Force release failed: {ex.Message}");
        }
    }
}
