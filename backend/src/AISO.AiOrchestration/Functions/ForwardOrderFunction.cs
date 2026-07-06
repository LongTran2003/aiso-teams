using System.Text.Json;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Forwards a Sales Order to another user for review/approval.
/// Maps to AI function schema <c>ForwardOrder</c>.
/// Sprint 2-3: returns mock success. Sprint 4: calls SAP substitution service.
/// </summary>
public sealed class ForwardOrderFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly ILogger<ForwardOrderFunction> _logger;

    public ForwardOrderFunction(ISapClient sap, ILogger<ForwardOrderFunction> logger)
    {
        _sap = sap;
        _logger = logger;
    }

    public string Name => "ForwardOrder";

    public string Description =>
        "Forward a sales order to another user for further review or approval.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "order_id": {
              "type": "string",
              "description": "The unique sales order identifier (e.g. '0000005001')."
            },
            "forward_to_user": {
              "type": "string",
              "description": "Target recipient username, name, or email."
            }
          },
          "required": ["order_id", "forward_to_user"]
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
    {
        var orderId = parameters.TryGetProperty("order_id", out var p)
                      && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

        var forwardTo = parameters.TryGetProperty("forward_to_user", out var f)
                        && f.ValueKind == JsonValueKind.String
            ? f.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(orderId))
        {
            return FunctionResult.Fail("Missing required parameter: order_id");
        }

        if (string.IsNullOrWhiteSpace(forwardTo))
        {
            return FunctionResult.Fail("Missing required parameter: forward_to_user");
        }

        // Authorization Check
        var allowedManagers = new[] { "DEV-249", "DEV-001", "DEV-002" };
        if (!allowedManagers.Contains(requestingSapUser.ToUpperInvariant()))
        {
            _logger.LogWarning("AUDIT: User {User} attempted to forward order {OrderId} but does not have manager role.", requestingSapUser, orderId);
            return FunctionResult.Fail("Authorization failed: You do not have the required 'Manager' role to forward sales orders.");
        }

        _logger.LogInformation(
            "ForwardOrder: orderId={OrderId}, forwardTo={ForwardTo}, sapUser={SapUser}", orderId, forwardTo, requestingSapUser);

        try
        {
            // Call SAP RAP action
            var updatedOrder = await _sap.ForwardOrderAsync(orderId, forwardTo, requestingSapUser, ct);
            
            // Audit Log
            _logger.LogInformation("AUDIT: User {User} successfully forwarded order {OrderId} to {ForwardTo}", requestingSapUser, orderId, forwardTo);

            var result = new
            {
                order_id = updatedOrder.SoNumber,
                action = "Forwarded",
                forward_to_user = forwardTo,
                message = $"Sales order {updatedOrder.SoNumber} has been forwarded to {forwardTo}. Status is {updatedOrder.Status}."
            };

            return FunctionResult.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to forward order {OrderId}", orderId);
            return FunctionResult.Fail($"Failed to forward order in SAP: {ex.Message}");
        }
    }
}

