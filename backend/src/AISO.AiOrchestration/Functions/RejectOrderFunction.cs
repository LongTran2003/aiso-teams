using System.Text.Json;
using AISO.Domain.Approvals;
using AISO.Domain.SalesOrders;
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
    private readonly IOrderApprovalService _approvals;
    private readonly ILogger<RejectOrderFunction> _logger;

    public RejectOrderFunction(
        ISapClient sap,
        IOrderApprovalService approvals,
        ILogger<RejectOrderFunction> logger)
    {
        _sap = sap;
        _approvals = approvals;
        _logger = logger;
    }

    public string Name => "RejectOrder";

    public string Description =>
        "Reject or cancel a sales order in the SAP ERP system with a short reason code.";

    public string ParametersJsonSchema
    {
        get
        {
            var enumJson = string.Join(", ", SalesOrderRejectionReasons.Codes.Select(c => $"\"{c}\""));
            var titles = string.Join("; ", SalesOrderRejectionReasons.All.Select(r => $"{r.Code}={r.Title}"));
            return $$"""
                {
                  "type": "object",
                  "properties": {
                    "order_id": {
                      "type": "string",
                      "description": "The unique sales order identifier (e.g. '0000005001')."
                    },
                    "reason_code": {
                      "type": "string",
                      "enum": [{{enumJson}}],
                      "description": "Short rejection reason code. {{titles}}"
                    }
                  },
                  "required": ["order_id", "reason_code"]
                }
                """;
        }
    }

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

        var canonicalReason = SalesOrderRejectionReasons.ToCanonicalCode(reasonCode);
        var reason = SalesOrderRejectionReasons.All.First(r => r.Code == canonicalReason);
        var sapReasonCode = reason.SapAbgru;

        // Authorization Check
        var allowedManagers = new[] { "DEV-249" };
        if (!allowedManagers.Contains(requestingSapUser.ToUpperInvariant()))
        {
            _logger.LogWarning("AUDIT: User {User} attempted to reject order {OrderId} but does not have manager role.", requestingSapUser, orderId);
            return FunctionResult.Fail("Authorization failed: You do not have the required 'Manager' role to reject sales orders.");
        }

        _logger.LogInformation(
            "RejectOrder: orderId={OrderId}, reasonCode={ReasonCode}, sapAbgru={SapAbgru}, sapUser={SapUser}",
            orderId, canonicalReason, sapReasonCode, requestingSapUser);

        try
        {
            var existing = await _sap.GetSalesOrderByIdAsync(orderId, ct);
            if (existing is null)
            {
                return FunctionResult.Fail($"Sales order {orderId} was not found in SAP.");
            }

            if (SalesOrderWorkflow.BlocksReleaseRejectForward(existing.Status))
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildBlockedMessage(existing.Status, "Reject"));
            }

            var pending = await _approvals.GetPendingBySoNumberAsync(existing.SoNumber, ct);
            if (pending is not null)
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildPendingApprovalBlockedMessage(
                        "Reject",
                        pending.RequestedBySapUser));
            }

            var updatedOrder = await _sap.RejectOrderAsync(orderId, sapReasonCode, requestingSapUser, ct);

            // Audit Log
            _logger.LogInformation(
                "AUDIT: User {User} successfully rejected order {OrderId} with reason: {Reason}",
                requestingSapUser, orderId, canonicalReason);

            var result = new
            {
                order_id = updatedOrder.SoNumber,
                action = "Rejected",
                reason_code = canonicalReason,
                reason_title = reason.Title,
                message = $"Sales order {updatedOrder.SoNumber} has been rejected ({reason.Title}). Status is now {updatedOrder.Status}."
            };

            return FunctionResult.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject order {OrderId}", orderId);
            return FunctionResult.Fail($"Failed to reject order in SAP: {ex.Message}");
        }
    }
}
