using System.Text.Json;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>Manager/Admin: reassign SO ownership in SAP zaiso_so_map.</summary>
public sealed class ReassignOwnerFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly ILogger<ReassignOwnerFunction> _logger;

    public ReassignOwnerFunction(ISapClient sap, ILogger<ReassignOwnerFunction> logger)
    {
        _sap = sap;
        _logger = logger;
    }

    public string Name => "ReassignOwner";

    public string Description =>
        "Reassign sales order ownership to another SAP user. Manager or Admin only.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "order_id": {
              "type": "string",
              "description": "The unique sales order identifier."
            },
            "new_owner": {
              "type": "string",
              "description": "SAP user ID of the new owner (e.g. DEV-024)."
            }
          },
          "required": ["order_id", "new_owner"]
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
    {
        var orderId = parameters.TryGetProperty("order_id", out var p)
                      && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
        var newOwner = parameters.TryGetProperty("new_owner", out var n)
                       && n.ValueKind == JsonValueKind.String
            ? n.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(orderId))
            return FunctionResult.Fail("Missing required parameter: order_id");
        if (string.IsNullOrWhiteSpace(newOwner))
            return FunctionResult.Fail("Missing required parameter: new_owner");

        try
        {
            var updated = await _sap.ReassignOwnerAsync(orderId, newOwner, requestingSapUser, ct);
            _logger.LogInformation(
                "ReassignOwner: so={SoNumber} by={User} -> {NewOwner}",
                updated.SoNumber, requestingSapUser, newOwner);
            return FunctionResult.Ok(new
            {
                order_id = updated.SoNumber,
                action = "Reassigned",
                new_owner = newOwner,
                message = $"Sales order {updated.SoNumber} ownership reassigned to {newOwner}."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReassignOwner failed for {OrderId}", orderId);
            return FunctionResult.Fail($"Reassign owner failed: {ex.Message}");
        }
    }
}
