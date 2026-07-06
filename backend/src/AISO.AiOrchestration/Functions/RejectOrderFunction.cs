using System.Text.Json;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Rejects a Sales Order in SAP with a reason code.
/// Maps to AI function schema <c>RejectOrder</c>.
/// Sprint 3: calls SAP RAP action.
/// </summary>
public sealed class RejectOrderFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly ILogger<RejectOrderFunction> _logger;

    public RejectOrderFunction(ISapClient sap, ILogger<RejectOrderFunction> logger)
    {
        _sap = sap;
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

    public async Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
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
            return FunctionResult.Fail("Missing required parameter: order_id");
        }

        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            return FunctionResult.Fail("Missing required parameter: reason_code");
        }

        // Authorization Check
        var allowedManagers = new[] { "DEV-249", "DEV-001", "DEV-002" };
        if (!allowedManagers.Contains(requestingSapUser.ToUpperInvariant()))
        {
            _logger.LogWarning("AUDIT: User {User} attempted to reject order {OrderId} but does not have manager role.", requestingSapUser, orderId);
            return FunctionResult.Fail("Authorization failed: You do not have the required 'Manager' role to reject sales orders.");
        }

        _logger.LogInformation(
            "RejectOrder: orderId={OrderId}, reasonCode={ReasonCode}, sapUser={SapUser}", orderId, reasonCode, requestingSapUser);

        // Map AI friendly reason_codes to SAP 2-char ABGRU (Reason for Rejection)
        var sapReasonCode = reasonCode.ToUpperInvariant() switch
        {
            "PRICE_ISSUE" => "02", // Too expensive
            "OUT_OF_STOCK" => "04", // Not in stock
            _ => "03" // Other / Customer Cancellation
        };

        try
        {
            var updatedOrder = await _sap.CancelOrderAsync(orderId, sapReasonCode, requestingSapUser, ct);
            
            // Audit Log
            _logger.LogInformation("AUDIT: User {User} successfully rejected order {OrderId} with reason: {Reason}", requestingSapUser, orderId, reasonCode);

            var result = new
            {
                order_id = updatedOrder.SoNumber,
                action = "Canceled",
                reason_code = reasonCode,
                message = $"Sales order {updatedOrder.SoNumber} has been canceled (reason: {reasonCode}). Status is now {updatedOrder.Status}."
            };

            return FunctionResult.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel order {OrderId}", orderId);
            return FunctionResult.Fail($"Failed to cancel order in SAP: {ex.Message}");
        }
    }
}

