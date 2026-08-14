using System.Text.Json;
using AISO.Domain.Users;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Admin: pre-assigns a Teams email to an SAP User ID in the allow-list.
/// </summary>
public sealed class PreAssignUserFunction : IFunction
{
    private readonly IBotUserAdminService _users;
    private readonly ILogger<PreAssignUserFunction> _logger;

    public PreAssignUserFunction(IBotUserAdminService users, ILogger<PreAssignUserFunction> logger)
    {
        _users = users;
        _logger = logger;
    }

    public string Name => "PreAssignUser";

    public string Description =>
        "Admin only: adds a user's Teams email to the allow-list for a specific SAP User ID. " +
        "Use this when adding a new user to the system. " +
        "Required inputs: teams_email, sap_user_id, role, sales_org.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "teams_email": {
              "type": "string",
              "description": "The Microsoft Teams email of the user (e.g. user@domain.com)."
            },
            "sap_user_id": {
              "type": "string",
              "description": "SAP user id to assign (e.g. DEV-250)."
            },
            "role": {
              "type": "string",
              "enum": ["Employee", "Manager", "Admin"],
              "description": "Role to grant."
            },
            "sales_org": {
              "type": "string",
              "description": "Sales Organization (e.g. TV01). Null for Admin or if none."
            }
          },
          "required": ["teams_email", "sap_user_id", "role"],
          "additionalProperties": false
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(
        JsonElement parameters,
        string requestingSapUser,
        CancellationToken ct = default)
    {
        var email = parameters.TryGetProperty("teams_email", out var pEmail)
                        && pEmail.ValueKind == JsonValueKind.String
            ? pEmail.GetString()
            : null;

        var sapUserId = parameters.TryGetProperty("sap_user_id", out var pSap)
                        && pSap.ValueKind == JsonValueKind.String
            ? pSap.GetString()
            : null;

        var roleStr = parameters.TryGetProperty("role", out var pRole)
                        && pRole.ValueKind == JsonValueKind.String
            ? pRole.GetString()
            : null;

        var salesOrg = parameters.TryGetProperty("sales_org", out var pOrg)
                        && pOrg.ValueKind == JsonValueKind.String
            ? pOrg.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(email))
            return FunctionResult.Fail("Missing required parameter: teams_email", "VALIDATION");
        if (string.IsNullOrWhiteSpace(sapUserId))
            return FunctionResult.Fail("Missing required parameter: sap_user_id", "VALIDATION");
        if (!Enum.TryParse<UserRole>(roleStr, true, out var role))
            return FunctionResult.Fail($"Invalid role: {roleStr}. Use Employee, Manager, or Admin.", "VALIDATION");

        _logger.LogInformation(
            "PreAssignUser: by={Admin} targetEmail={Email} targetSap={Sap}",
            requestingSapUser, email, sapUserId);

        try
        {
            var summary = await _users.PreAssignAccessAsync(sapUserId, email, role, salesOrg, ct);
            return FunctionResult.Ok(new {
                Message = $"Successfully allowed list for {email} -> {sapUserId}.",
                User = summary
            });
        }
        catch (InvalidOperationException ex)
        {
            return FunctionResult.Fail(ex.Message, "VALIDATION");
        }
    }
}
