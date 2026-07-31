using System.Text.Json;
using AISO.Domain.Approvals;
using AISO.Domain.SalesOrders;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Rejects a Sales Order in SAP with a reason code.
/// Maps to AI function schema <c>RejectOrder</c>.
/// Role gating is <see cref="RolePolicy"/> (Employee+); ownership / pending checks below.
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
            return FunctionResult.Fail("Missing required parameter: order_id", "VALIDATION");
        }

        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            return FunctionResult.Fail("Missing required parameter: reason_code", "VALIDATION");
        }

        var canonicalReason = SalesOrderRejectionReasons.ToCanonicalCode(reasonCode);
        var reason = SalesOrderRejectionReasons.All.First(r => r.Code == canonicalReason);
        var sapReasonCode = reason.SapAbgru;

        _logger.LogInformation(
            "RejectOrder: orderId={OrderId}, reasonCode={ReasonCode}, sapAbgru={SapAbgru}, sapUser={SapUser}",
            orderId, canonicalReason, sapReasonCode, requestingSapUser);

        try
        {
            var existing = await _sap.GetSalesOrderByIdAsync(orderId, ct);
            if (existing is null)
            {
                return FunctionResult.Fail($"Sales order {orderId} was not found in SAP.", "NOT_FOUND");
            }

            if (SalesOrderWorkflow.BlocksReject(existing.Status))
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildBlockedMessage(existing.Status, "Reject"),
                    "VALIDATION");
            }

            if (existing.HasInvalidMaterial)
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildInvalidMaterialBlockedMessage("Reject"),
                    "VALIDATION");
            }

            if (!SalesOrderWorkflow.IsCurrentOwner(existing.OwnerSapUser, requestingSapUser)
                && !string.IsNullOrWhiteSpace(existing.OwnerSapUser))
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildNotOwnerBlockedMessage("Reject", existing.OwnerSapUser),
                    "VALIDATION");
            }

            var pending = await _approvals.GetPendingBySoNumberAsync(existing.SoNumber, ct);
            if (pending is not null)
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildPendingApprovalBlockedMessage(
                        "Reject",
                        pending.RequestedBySapUser),
                    "VALIDATION");
            }

            var updatedOrder = await _sap.RejectOrderAsync(orderId, sapReasonCode, requestingSapUser, ct);

            _logger.LogInformation(
                "AUDIT: User {User} successfully rejected order {OrderId} with reason: {Reason}",
                requestingSapUser, orderId, canonicalReason);

            var displayedSo = updatedOrder.SoNumber is "UNKNOWN" or null or ""
                ? orderId
                : updatedOrder.SoNumber;

            var result = new
            {
                order_id = displayedSo,
                action = "Rejected",
                reason_code = canonicalReason,
                reason_title = reason.Title,
                message = $"Sales order {displayedSo} has been rejected ({reason.Title}). Status is now {updatedOrder.Status}."
            };

            return FunctionResult.Ok(result);
        }
        catch (SapODataException sapEx)
        {
            _logger.LogError(sapEx, "SAP business error rejecting order {OrderId}", orderId);
            return FunctionResult.Fail(sapEx.Message, "SAP_ERROR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject order {OrderId}", orderId);
            return FunctionResult.Fail($"Failed to reject order in SAP: {ex.Message}", "ACTION_FAILED");
        }
    }
}
