using System.Text.Json;
using AISO.Domain.Approvals;
using AISO.Domain.Users;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Checker step: Manager rejects a pending release request (no SAP release).
/// </summary>
public sealed class RejectApprovalFunction : IFunction
{
    private readonly IOrderApprovalService _approvals;
    private readonly IUserScopeLookup _scope;
    private readonly ILogger<RejectApprovalFunction> _logger;

    public RejectApprovalFunction(
        IOrderApprovalService approvals,
        IUserScopeLookup scope,
        ILogger<RejectApprovalFunction> logger)
    {
        _approvals = approvals;
        _scope = scope;
        _logger = logger;
    }

    public string Name => "RejectApproval";

    public string Description =>
        "Reject a pending release-approval request. Manager/Admin only. Does not reject the sales order in SAP.";

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
              "description": "Reason for rejecting the approval request."
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

            var approval = await _approvals.RejectAsync(
                orderId,
                requestingSapUser,
                salesOrg,
                isAdmin: role == UserRole.Admin,
                comment,
                ct);

            _logger.LogInformation(
                "RejectApproval: so={SoNumber} by={User}",
                approval.SoNumber, requestingSapUser);

            return FunctionResult.Ok(new
            {
                order_id = approval.SoNumber,
                action = "ApprovalRejected",
                comment,
                message = $"Release request for sales order {approval.SoNumber} was rejected."
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
            _logger.LogError(ex, "Failed to reject approval for {OrderId}", orderId);
            return FunctionResult.Fail($"Failed to reject approval: {ex.Message}");
        }
    }
}
