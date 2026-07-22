using System.Text.Json;
using AISO.Domain.Approvals;
using AISO.Domain.Users;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Lists pending release requests for the caller's sales organization (Manager/Admin).
/// </summary>
public sealed class GetPendingApprovalsFunction : IFunction
{
    private readonly IOrderApprovalService _approvals;
    private readonly IUserScopeLookup _scope;
    private readonly ILogger<GetPendingApprovalsFunction> _logger;

    public GetPendingApprovalsFunction(
        IOrderApprovalService approvals,
        IUserScopeLookup scope,
        ILogger<GetPendingApprovalsFunction> logger)
    {
        _approvals = approvals;
        _scope = scope;
        _logger = logger;
    }

    public string Name => "GetPendingApprovals";

    public string Description =>
        "List sales orders waiting for release approval. Manager sees their VKORG; Admin sees all.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {},
          "required": []
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
    {
        var role = await _scope.GetRoleBySapUserAsync(requestingSapUser, ct);
        var salesOrg = role == UserRole.Admin
            ? null
            : await _scope.GetSalesOrgBySapUserAsync(requestingSapUser, ct);

        var pending = await _approvals.GetPendingAsync(salesOrg, ct);

        _logger.LogInformation(
            "GetPendingApprovals: user={User} role={Role} salesOrg={SalesOrg} count={Count}",
            requestingSapUser, role, salesOrg, pending.Count);

        if (pending.Count == 0)
        {
            return FunctionResult.Ok(new
            {
                count = 0,
                message = "No pending release approvals.",
                items = Array.Empty<object>()
            });
        }

        return FunctionResult.Ok(new
        {
            count = pending.Count,
            message = $"Found {pending.Count} pending release request(s).",
            items = pending.Select(a => new
            {
                order_id = a.SoNumber,
                requested_by = a.RequestedBySapUser,
                sales_org = a.SalesOrg,
                comment = a.Comment,
                requested_at = a.RequestedAt.ToString("u")
            }).ToList()
        });
    }
}
