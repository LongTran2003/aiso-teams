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

        var dto = new RevokeDelegationDto(
            RequestingTeamsUser: requestingSapUser,
            DelegationId: delegationId ?? delegateUser!); // fallback if ID is missing

        try
        {
            await _sapClient.RevokeDelegationAsync(dto, ct);

            // Cập nhật local DB
            if (!string.IsNullOrWhiteSpace(delegateUser))
            {
                await _scope.SetDelegatedBySapUserAsync(delegateUser, null, null, null, ct);
            }

            return FunctionResult.Ok(new
            {
                action = "Revoked",
                message = "Successfully revoked delegation."
            });
        }
        catch (SapODataException ex)
        {
            return FunctionResult.Fail($"SAP rejected revocation: {ex.Message}", "VALIDATION");
        }
        catch (Exception ex)
        {
            return FunctionResult.Fail($"Error revoking delegation: {ex.Message}");
        }
    }
}
