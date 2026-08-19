using System.Text.Json;
using System.Text.Json.Serialization;

using AISO.Domain.Approvals;
using AISO.Domain.Users;
using AISO.SapIntegration;

namespace AISO.AiOrchestration.Functions;

public class RevokeDelegationFunction : IFunction
{
    public string Name => "RevokeDelegation";
    public string Description => "Revokes an existing approval delegation in SAP and local database.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "delegationId": {
              "type": "string",
              "description": "The unique ID of the delegation to revoke."
            },
            "delegateUser": {
              "type": "string",
              "description": "The SAP User ID of the delegate (fallback if delegation ID is not known)."
            }
          }
        }
        """;

    public UserRole MinimumRole => UserRole.Manager;

    private readonly ISapClient _sapClient;
    private readonly IUserScopeLookup _scope;

    public RevokeDelegationFunction(ISapClient sapClient, IUserScopeLookup scope)
    {
        _sapClient = sapClient;
        _scope = scope;
    }

    public async Task<FunctionResult> ExecuteAsync(
        JsonElement parameters,
        string requestingSapUser,
        CancellationToken ct)
    {
        var delegationId = parameters.TryGetProperty("delegationId", out var idProp) ? idProp.GetString() : null;
        var delegateUser = parameters.TryGetProperty("delegateUser", out var uProp) ? uProp.GetString() : null;

        if (string.IsNullOrWhiteSpace(delegationId) && string.IsNullOrWhiteSpace(delegateUser))
            return FunctionResult.Fail("Please provide the delegation ID or delegate user name to revoke.");

        var targetUser = delegateUser ?? delegationId!;

        try
        {
            // Validate if the targetUser is actually delegated
            var delegateeInfo = await _scope.GetDelegationInfoAsync(targetUser, ct);
            if (string.IsNullOrWhiteSpace(delegateeInfo.DelegatorSapUser))
            {
                return FunctionResult.Fail($"User {targetUser} does not currently have any active delegation.");
            }

            // Validate permissions: Only the original delegator or Admin can revoke.
            var requestingRole = await _scope.GetRoleBySapUserAsync(requestingSapUser, ct);
            if (requestingRole < UserRole.Admin && !string.Equals(delegateeInfo.DelegatorSapUser, requestingSapUser, StringComparison.OrdinalIgnoreCase))
            {
                return FunctionResult.Fail($"You cannot revoke this delegation. Only the original delegator ({delegateeInfo.DelegatorSapUser}) or an Admin can revoke it.", "UNAUTHORIZED");
            }

            return FunctionResult.Ok(new ConfirmRevokeDelegationResponse(
                targetUser,
                delegationId,
                delegateeInfo.DelegatorSapUser
            ));
        }
        catch (Exception ex)
        {
            return FunctionResult.Fail($"Error verifying delegation: {ex.Message}");
        }
    }
}

/// <summary>
/// Payload telling the bot to show <c>confirm-revoke-delegation</c>.
/// </summary>
public sealed record ConfirmRevokeDelegationResponse(
    string DelegateUser,
    string? DelegationId,
    string? DelegatorUser);
