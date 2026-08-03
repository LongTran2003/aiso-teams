using System.Text.Json;
using AISO.Domain.Users;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Admin: prepare manage-user card for a linked SAP user (does not write until card confirm).
/// </summary>
public sealed class ManageBotUserFunction : IFunction
{
    private readonly IBotUserAdminService _users;
    private readonly ILogger<ManageBotUserFunction> _logger;

    public ManageBotUserFunction(IBotUserAdminService users, ILogger<ManageBotUserFunction> logger)
    {
        _users = users;
        _logger = logger;
    }

    public string Name => "ManageBotUser";

    public string Description =>
        "Admin only: open the manage-user card to change Role and/or SalesOrg for a linked SAP user. " +
        "Use for 'manage user DEV-249', 'set role', 'set sales org'. Does not save until the Admin confirms on the card.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "sap_user_id": {
              "type": "string",
              "description": "SAP user id to manage (e.g. DEV-249)."
            }
          },
          "required": ["sap_user_id"],
          "additionalProperties": false
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(
        JsonElement parameters,
        string requestingSapUser,
        CancellationToken ct = default)
    {
        var sapUserId = parameters.TryGetProperty("sap_user_id", out var p)
                        && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(sapUserId))
            return FunctionResult.Fail("Missing required parameter: sap_user_id", "VALIDATION");

        var user = await _users.GetBySapUserIdAsync(sapUserId, ct);
        if (user is null)
        {
            return FunctionResult.Fail(
                $"No linked Teams user found for SAP ID {sapUserId.Trim().ToUpperInvariant()}.",
                "NOT_FOUND");
        }

        _logger.LogInformation(
            "ManageBotUser prepare: by={Admin} target={Target}",
            requestingSapUser, user.SapUserId);

        return FunctionResult.Ok(new ManageBotUserResponse(user));
    }
}

public sealed record ManageBotUserResponse(BotUserSummary User);
