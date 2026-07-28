using System.Text.Json;
using AISO.Domain.Approvals;
using AISO.Domain.SalesOrders;
using AISO.Domain.Users;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Checker step: Manager approves a pending release via SAP <c>approveOrder</c>
/// (Phase A: role from ZAISO_USER_ROLE + real release), then clears Postgres pending.
/// </summary>
public sealed class ApproveOrderFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly IOrderApprovalService _approvals;
    private readonly IUserScopeLookup _scope;
    private readonly ILogger<ApproveOrderFunction> _logger;

    public ApproveOrderFunction(
        ISapClient sap,
        IOrderApprovalService approvals,
        IUserScopeLookup scope,
        ILogger<ApproveOrderFunction> logger)
    {
        _sap = sap;
        _approvals = approvals;
        _scope = scope;
        _logger = logger;
    }

    public string Name => "ApproveOrder";

    public string Description =>
        "Approve a pending release request and release the sales order in SAP. Manager/Admin only.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "order_id": {
              "type": "string",
              "description": "The unique sales order identifier (e.g. '0000005001')."
            },
            "comment": {
              "type": "string",
              "description": "Optional approval comment."
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

        var comment = parameters.TryGetProperty("comment", out var c)
                      && c.ValueKind == JsonValueKind.String
            ? c.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(orderId))
        {
            return FunctionResult.Fail("Missing required parameter: order_id");
        }

        try
        {
            var role = await _scope.GetRoleBySapUserAsync(requestingSapUser, ct);
            var salesOrg = await _scope.GetSalesOrgBySapUserAsync(requestingSapUser, ct);
            var isAdmin = role == UserRole.Admin;

            var pending = await _approvals.GetPendingBySoNumberAsync(orderId, ct);
            if (pending is null)
            {
                return FunctionResult.Fail($"No pending release request found for sales order {orderId}.");
            }

            if (!isAdmin
                && !string.IsNullOrWhiteSpace(salesOrg)
                && !string.IsNullOrWhiteSpace(pending.SalesOrg)
                && !string.Equals(pending.SalesOrg, salesOrg, StringComparison.OrdinalIgnoreCase))
            {
                return FunctionResult.Fail(
                    $"Order {pending.SoNumber} belongs to sales org {pending.SalesOrg}; your scope is {salesOrg}.");
            }

            var existing = await _sap.GetSalesOrderByIdAsync(pending.SoNumber, ct);
            if (existing is not null && SalesOrderWorkflow.BlocksReleaseRejectForward(existing.Status))
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildBlockedMessage(existing.Status, "Approve / release"));
            }

            // Phase A: SAP approveOrder enforces ZAISO_USER_ROLE and performs release (no ownership).
            var updatedOrder = await _sap.ApproveOrderAsync(pending.SoNumber, requestingSapUser, ct);

            var approval = await _approvals.ApproveAsync(
                pending.SoNumber,
                requestingSapUser,
                salesOrg,
                isAdmin,
                comment,
                ct);

            _logger.LogInformation(
                "ApproveOrder: so={SoNumber} by={User} (was requested by {Requester})",
                updatedOrder.SoNumber, requestingSapUser, approval.RequestedBySapUser);

            return FunctionResult.Ok(new
            {
                order_id = updatedOrder.SoNumber,
                action = "Approved",
                comment,
                message = $"Sales order {updatedOrder.SoNumber} was approved and released. Status is now {updatedOrder.Status}."
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return FunctionResult.Fail(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return FunctionResult.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve order {OrderId}", orderId);
            return FunctionResult.Fail($"Failed to approve order: {ex.Message}");
        }
    }
}
