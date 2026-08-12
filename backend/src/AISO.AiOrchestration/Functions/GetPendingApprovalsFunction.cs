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
        var delegatedBy = await _scope.GetDelegatedBySapUserAsync(requestingSapUser, ct);

        if (role < UserRole.Manager && string.IsNullOrWhiteSpace(delegatedBy))
        {
            return FunctionResult.Fail("Only Manager or Admin can view pending approvals (or a delegated user).", "UNAUTHORIZED");
        }

        var salesOrg = role == UserRole.Admin
            ? null
            : await _scope.GetSalesOrgBySapUserAsync(requestingSapUser, ct);

        var pending = await _approvals.GetPendingAsync(salesOrg, ct);

        _logger.LogInformation(
            "GetPendingApprovals: user={User} role={Role} salesOrg={SalesOrg} count={Count}",
            requestingSapUser, role, salesOrg, pending.Count);

        if (pending.Count == 0)
        {
            var emptyMessage = string.IsNullOrWhiteSpace(salesOrg)
                ? "No pending release approvals."
                : $"No pending release approvals in sales org {salesOrg}. " +
                  "If an employee just requested release, confirm the order's VKORG matches your Manager SalesOrg.";

            return FunctionResult.Ok(new GetPendingApprovalsResponse(
                0,
                emptyMessage,
                Array.Empty<PendingApprovalItem>()));
        }

        return FunctionResult.Ok(new GetPendingApprovalsResponse(
            pending.Count,
            $"Found {pending.Count} pending release request(s).",
            pending.Select(a => new PendingApprovalItem(
                a.SoNumber,
                a.RequestedBySapUser,
                a.SalesOrg ?? string.Empty,
                a.Comment ?? string.Empty,
                a.RequestedAt.ToString("u"))).ToList()));
    }
}

public sealed record PendingApprovalItem(
    string OrderId,
    string RequestedBy,
    string SalesOrg,
    string Comment,
    string RequestedAt);

public sealed record GetPendingApprovalsResponse(
    int Count,
    string Message,
    IReadOnlyList<PendingApprovalItem> Items);
