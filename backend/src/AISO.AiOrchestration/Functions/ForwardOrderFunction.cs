using System.Text.Json;
using AISO.Domain.Approvals;
using AISO.Domain.SalesOrders;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Forwards a Sales Order to another user for review/approval.
/// Maps to AI function schema <c>ForwardOrder</c>.
/// Ownership is enforced by SAP <c>zaiso_so_map</c> (and by BE when <c>OwnerSapUser</c> is present).
/// </summary>
public sealed class ForwardOrderFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly IOrderApprovalService _approvals;
    private readonly ILogger<ForwardOrderFunction> _logger;

    public ForwardOrderFunction(
        ISapClient sap,
        IOrderApprovalService approvals,
        ILogger<ForwardOrderFunction> logger)
    {
        _sap = sap;
        _approvals = approvals;
        _logger = logger;
    }

    public string Name => "ForwardOrder";

    public string Description =>
        "Forward a sales order you own to another SAP user. Transfers ownership; you will no longer own the order.";

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
              "description": "Target recipient SAP user id (e.g. DEV-300)."
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

        // Role gating is RolePolicy (Employee+). Ownership is enforced below / in SAP.
        _logger.LogInformation(
            "ForwardOrder: orderId={OrderId}, forwardTo={ForwardTo}, sapUser={SapUser}", orderId, forwardTo, requestingSapUser);

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
                    SalesOrderWorkflow.BuildBlockedMessage(existing.Status, "Forward"));
            }

            var pending = await _approvals.GetPendingBySoNumberAsync(existing.SoNumber, ct);
            if (pending is not null)
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildPendingApprovalBlockedMessage(
                        "Forward",
                        pending.RequestedBySapUser));
            }

            if (!SalesOrderWorkflow.IsCurrentOwner(existing.OwnerSapUser, requestingSapUser)
                && !string.IsNullOrWhiteSpace(existing.OwnerSapUser))
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildNotOwnerBlockedMessage("Forward", existing.OwnerSapUser));
            }

            var updatedOrder = await _sap.ForwardOrderAsync(orderId, forwardTo, requestingSapUser, ct);

            _logger.LogInformation("AUDIT: User {User} successfully forwarded order {OrderId} to {ForwardTo}", requestingSapUser, orderId, forwardTo);

            var result = new
            {
                order_id = updatedOrder.SoNumber,
                action = "Forwarded",
                forward_to_user = forwardTo,
                message = $"Ownership of sales order {updatedOrder.SoNumber} transferred to {forwardTo}. You no longer own this order."
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
