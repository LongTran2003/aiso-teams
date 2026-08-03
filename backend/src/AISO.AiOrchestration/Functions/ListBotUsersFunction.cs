using System.Text.Json;
using AISO.Domain.Users;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>Admin: list linked bot users (role + SalesOrg).</summary>
public sealed class ListBotUsersFunction : IFunction
{
    private readonly IBotUserAdminService _users;
    private readonly ILogger<ListBotUsersFunction> _logger;

    public ListBotUsersFunction(IBotUserAdminService users, ILogger<ListBotUsersFunction> logger)
    {
        _users = users;
        _logger = logger;
    }

    public string Name => "ListBotUsers";

    public string Description =>
        "Admin only: list linked Teams↔SAP users with role and SalesOrg. " +
        "Use for 'list users', 'show users', 'manage users'.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(
        JsonElement parameters,
        string requestingSapUser,
        CancellationToken ct = default)
    {
        var users = await _users.ListLinkedUsersAsync(ct);
        _logger.LogInformation(
            "ListBotUsers: by={User} count={Count}", requestingSapUser, users.Count);

        return FunctionResult.Ok(new ListBotUsersResponse(users));
    }
}

public sealed record ListBotUsersResponse(IReadOnlyList<BotUserSummary> Users);
