using System.Text.Json;
using AISO.Domain.SalesOrders;
using AISO.Domain.Users;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Prepare cancel for a sales order (SAP <c>cancelOrder</c>).
/// Shows a confirmation card — SAP call runs on Adaptive Card <c>cancel_so_confirm</c>.
/// Employee: own SO only. Manager/Admin: any SO (Manager scoped by SalesOrg when set).
/// </summary>
public sealed class CancelOrderFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly IUserScopeLookup _scope;
    private readonly ILogger<CancelOrderFunction> _logger;

    public CancelOrderFunction(
        ISapClient sap,
        IUserScopeLookup scope,
        ILogger<CancelOrderFunction> logger)
    {
        _sap = sap;
        _scope = scope;
        _logger = logger;
    }

    public string Name => "CancelOrder";

    public string Description =>
        "Cancel a sales order in SAP. Employee may cancel their own order; " +
        "Manager/Admin may cancel others. Returns a confirmation card — does not cancel until confirmed.";

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
              "description": "Optional cancel reason to prefill on the confirm card."
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
            return FunctionResult.Fail("Missing required parameter: order_id", "VALIDATION");

        try
        {
            var existing = await _sap.GetSalesOrderByIdAsync(orderId, ct);
            if (existing is null)
                return FunctionResult.Fail($"Sales order {orderId} was not found in SAP.", "NOT_FOUND");

            if (SalesOrderWorkflow.BlocksReject(existing.Status))
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildBlockedMessage(existing.Status, "Cancel"),
                    "VALIDATION");
            }

            if (existing.HasInvalidMaterial)
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildInvalidMaterialBlockedMessage("Cancel"),
                    "VALIDATION");
            }

            var role = await _scope.GetRoleBySapUserAsync(requestingSapUser, ct);
            var authError = await ValidateCancelAuthorizationAsync(existing, requestingSapUser, role, ct);
            if (authError is not null)
                return FunctionResult.Fail(authError, "VALIDATION");

            _logger.LogInformation(
                "CancelOrder confirm step: so={SoNumber} by={User} role={Role}",
                existing.SoNumber, requestingSapUser, role);

            return FunctionResult.Ok(new ConfirmCancelOrderResponse(
                existing.SoNumber,
                string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CancelOrder prepare failed for {OrderId}", orderId);
            return FunctionResult.Fail($"Cancel order failed: {ex.Message}", "ACTION_FAILED");
        }
    }

    private async Task<string?> ValidateCancelAuthorizationAsync(
        SalesOrder order,
        string requestingSapUser,
        UserRole role,
        CancellationToken ct)
    {
        if (role >= UserRole.Admin)
            return null;

        if (role >= UserRole.Manager)
        {
            var delegationInfo = await _scope.GetDelegationInfoAsync(requestingSapUser, ct);
            var effectiveUserForOrg = !string.IsNullOrWhiteSpace(delegationInfo.DelegatorSapUser)
                ? delegationInfo.DelegatorSapUser
                : requestingSapUser;
            var managerOrg = await _scope.GetSalesOrgBySapUserAsync(effectiveUserForOrg, ct);
            if (!string.IsNullOrWhiteSpace(managerOrg)
                && !string.IsNullOrWhiteSpace(order.SalesOrg)
                && !string.Equals(managerOrg, order.SalesOrg, StringComparison.OrdinalIgnoreCase))
            {
                return $"Order {order.SoNumber} belongs to sales org {order.SalesOrg}; your scope is {managerOrg}.";
            }

            return null;
        }

        // Employee: own only
        if (!SalesOrderWorkflow.IsCurrentOwner(order.OwnerSapUser, requestingSapUser)
            && !string.IsNullOrWhiteSpace(order.OwnerSapUser))
        {
            return SalesOrderWorkflow.BuildNotOwnerBlockedMessage("Cancel", order.OwnerSapUser);
        }

        return null;
    }
}

/// <summary>Payload telling the bot to show <c>confirm-cancel.json</c>.</summary>
public sealed record ConfirmCancelOrderResponse(string SoNumber, string? Reason = null);
