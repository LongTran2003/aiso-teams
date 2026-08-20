using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using AISO.Domain.Approvals;
using AISO.Domain.SalesOrders;
using AISO.Domain.Users;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Bulk approves multiple pending release requests.
/// </summary>
public sealed class ApproveSelectedOrdersFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly IOrderApprovalService _approvals;
    private readonly IUserScopeLookup _scope;
    private readonly ILogger<ApproveSelectedOrdersFunction> _logger;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _config;

    public ApproveSelectedOrdersFunction(
        ISapClient sap,
        IOrderApprovalService approvals,
        IUserScopeLookup scope,
        ILogger<ApproveSelectedOrdersFunction> logger,
        Microsoft.Extensions.Configuration.IConfiguration config)
    {
        _sap = sap;
        _approvals = approvals;
        _scope = scope;
        _logger = logger;
        _config = config;
    }

    public string Name => "ApproveSelectedOrders";

    public string Description =>
        "Approve multiple pending release requests and release the sales orders in SAP. Manager/Admin only.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "order_ids": {
              "type": "array",
              "items": { "type": "string" },
              "description": "List of sales order IDs to approve."
            },
            "comment": {
              "type": "string",
              "description": "Optional common approval comment."
            }
          },
          "required": ["order_ids"]
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
    {
        var orderIds = new List<string>();
        if (parameters.TryGetProperty("order_ids", out var idsProp) && idsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in idsProp.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    var id = element.GetString();
                    if (!string.IsNullOrWhiteSpace(id))
                        orderIds.Add(id);
                }
            }
        }

        var comment = parameters.TryGetProperty("comment", out var c) && c.ValueKind == JsonValueKind.String
            ? c.GetString()
            : null;

        if (orderIds.Count == 0)
        {
            return FunctionResult.Fail("No order IDs provided for bulk approval.", "VALIDATION");
        }

        var role = await _scope.GetRoleBySapUserAsync(requestingSapUser, ct);
        var delegationInfo = await _scope.GetDelegationInfoAsync(requestingSapUser, ct);
        var effectiveUserForOrg = !string.IsNullOrWhiteSpace(delegationInfo.DelegatorSapUser)
            ? delegationInfo.DelegatorSapUser
            : requestingSapUser;
        var salesOrg = await _scope.GetSalesOrgBySapUserAsync(effectiveUserForOrg, ct);
        var isAdmin = role == UserRole.Admin;

        if (role < UserRole.Manager && string.IsNullOrWhiteSpace(delegationInfo.DelegatorSapUser))
        {
            return FunctionResult.Fail("Only Manager or Admin can approve release requests (or a delegated user).", "UNAUTHORIZED");
        }

        var successes = 0;
        var failures = new List<string>();

        foreach (var orderId in orderIds)
        {
            try
            {
                var pending = await _approvals.GetPendingBySoNumberAsync(orderId, ct);
                if (pending is null)
                {
                    failures.Add($"{orderId}: No pending request found.");
                    continue;
                }

                if (!isAdmin
                    && !string.IsNullOrWhiteSpace(salesOrg)
                    && !string.IsNullOrWhiteSpace(pending.SalesOrg)
                    && !string.Equals(pending.SalesOrg, salesOrg, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"{orderId}: Scope mismatch (Order {pending.SalesOrg} vs You {salesOrg}).");
                    continue;
                }

                var existing = await _sap.GetSalesOrderByIdAsync(pending.SoNumber, ct);
                if (existing is not null && SalesOrderWorkflow.BlocksReleaseRejectForward(existing.Status))
                {
                    failures.Add($"{orderId}: {SalesOrderWorkflow.BuildBlockedMessage(existing.Status, "Approve")}");
                    continue;
                }

                if (existing is { HasInvalidMaterial: true })
                {
                    failures.Add($"{orderId}: {SalesOrderWorkflow.BuildInvalidMaterialBlockedMessage("Approve")}");
                    continue;
                }

                if (!isAdmin && existing is not null)
                {
                    if (!string.IsNullOrWhiteSpace(delegationInfo.DelegatorSapUser) && delegationInfo.MaxAmount.HasValue)
                    {
                        if (existing.NetValue > delegationInfo.MaxAmount.Value)
                        {
                            failures.Add($"{orderId}: Exceeds delegation max amount limit ({delegationInfo.MaxAmount.Value:N0} {existing.Currency}).");
                            continue;
                        }
                    }

                    var thresholdError = ApprovalThresholdHelper.CheckThreshold(_config, existing.NetValue, existing.Currency);
                    if (thresholdError is not null)
                    {
                        failures.Add($"{orderId}: {thresholdError}");
                        continue;
                    }
                }

                var updatedOrder = await _sap.ApproveOrderAsync(pending.SoNumber, requestingSapUser, ct);

                await _approvals.ApproveAsync(
                    pending.SoNumber,
                    requestingSapUser,
                    salesOrg,
                    isAdmin,
                    comment,
                    ct);

                _logger.LogInformation("Bulk approve successful for so={SoNumber} by={User}", updatedOrder.SoNumber, requestingSapUser);
                successes++;
            }
            catch (SapODataException sapEx)
            {
                failures.Add($"{orderId}: SAP Error - {sapEx.Message}");
                _logger.LogError(sapEx, "Bulk approve failed for so={OrderId}", orderId);
            }
            catch (Exception ex)
            {
                failures.Add($"{orderId}: System Error - {ex.Message}");
                _logger.LogError(ex, "Bulk approve failed for so={OrderId}", orderId);
            }
        }

        var resultMsg = new StringBuilder($"Successfully approved {successes} order(s).");
        if (failures.Count > 0)
        {
            resultMsg.AppendLine($" Failed {failures.Count} order(s):");
            foreach (var fail in failures)
                resultMsg.AppendLine($"- {fail}");
        }

        return FunctionResult.Ok(new
        {
            action = "BulkApproved",
            successes,
            failures = failures.Count,
            message = resultMsg.ToString()
        });
    }
}
