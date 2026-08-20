using System.Text.Json;
using AISO.Domain.Users;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

public class ListDelegationsFunction : IFunction
{
    private readonly IUserScopeLookup _scope;
    private readonly ILogger<ListDelegationsFunction> _logger;

    public ListDelegationsFunction(IUserScopeLookup scope, ILogger<ListDelegationsFunction> logger)
    {
        _scope = scope;
        _logger = logger;
    }

    public string Name => "ListDelegations";

    public string Description => "List all currently active delegations. Use this when the user asks to see who is delegated, who they delegated to, or who has approval rights.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {},
            "required": []
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(
        JsonElement parameters,
        string requestingSapUser,
        CancellationToken ct)
    {
        var role = await _scope.GetRoleBySapUserAsync(requestingSapUser, ct);

        // If employee, they can't see delegations
        if (role < UserRole.Manager)
        {
            return FunctionResult.Fail("You do not have permission to view delegations. Only Managers and Admins can view delegations.", "VALIDATION");
        }

        // Managers can only see their own delegations. Admins see all.
        var filterUser = role == UserRole.Manager ? requestingSapUser : null;
        var delegations = await _scope.GetActiveDelegationsAsync(filterUser, ct);

        var list = delegations.Select(d =>
        {
            var timeRemaining = "Expired";
            if (d.ValidTo.HasValue)
            {
                var diff = d.ValidTo.Value - DateTimeOffset.UtcNow;
                if (diff.TotalSeconds > 0)
                {
                    timeRemaining = $"{(int)diff.TotalDays}d {diff.Hours}h {diff.Minutes}m";
                }
            }

            return new DelegationItem(
                d.DelegateUser,
                d.DelegateName,
                d.DelegatorUser,
                d.ValidTo?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A",
                timeRemaining,
                d.MaxAmount.HasValue ? d.MaxAmount.Value.ToString("N0") : "Unlimited");
        }).ToList();

        return FunctionResult.Ok(new ListDelegationsResponse(list));
    }
}

public sealed record DelegationItem(
    string DelegateUser,
    string DelegateName,
    string DelegatorUser,
    string ValidTo,
    string TimeRemaining,
    string MaxAmount);

public sealed record ListDelegationsResponse(
    IReadOnlyList<DelegationItem> Delegations);
