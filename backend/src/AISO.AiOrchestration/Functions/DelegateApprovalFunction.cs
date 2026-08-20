using System.Text.Json;
using System.Text.Json.Serialization;

using AISO.Domain.Approvals;
using AISO.Domain.Users;
using AISO.Domain.Notifications;
using AISO.SapIntegration;

namespace AISO.AiOrchestration.Functions;

public class DelegateApprovalFunction : IFunction
{
    public string Name => "DelegateApproval";
    public string Description => "Delegates the user's approval authority to another employee in SAP and local database.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "delegateUser": {
              "type": "string",
              "description": "SAP User ID of the employee receiving the delegation."
            },
            "validFrom": {
              "type": "string",
              "format": "date",
              "description": "Start date of delegation in YYYY-MM-DD format."
            },
            "validTo": {
              "type": "string",
              "format": "date",
              "description": "End date of delegation in YYYY-MM-DD format."
            },
            "reason": {
              "type": "string",
              "description": "Optional reason for delegation."
            },
            "maxAmount": {
              "type": "number",
              "description": "Optional maximum approval amount limit."
            },
            "currency": {
              "type": "string",
              "description": "Currency code for the max amount (e.g. VND, USD). Default is VND."
            }
          },
          "required": ["delegateUser", "validFrom", "validTo"]
        }
        """;

    public UserRole MinimumRole => UserRole.Manager;

    private readonly ISapClient _sapClient;
    private readonly IUserScopeLookup _scope;
    private readonly IEmailService _emailService;

    public DelegateApprovalFunction(ISapClient sapClient, IUserScopeLookup scope, IEmailService emailService)
    {
        _sapClient = sapClient;
        _scope = scope;
        _emailService = emailService;
    }

    public async Task<FunctionResult> ExecuteAsync(
        JsonElement parameters,
        string requestingSapUser,
        CancellationToken ct)
    {
        var delegateUser = parameters.TryGetProperty("delegateUser", out var dUser) ? dUser.GetString() : null;
        var validFrom = parameters.TryGetProperty("validFrom", out var vFrom) ? vFrom.GetString() : null;
        var validTo = parameters.TryGetProperty("validTo", out var vTo) ? vTo.GetString() : null;
        var reason = parameters.TryGetProperty("reason", out var r) ? r.GetString() : null;
        var currencyRaw = parameters.TryGetProperty("currency", out var c) ? c.GetString() : null;
        var currency = string.IsNullOrWhiteSpace(currencyRaw) ? "VND" : currencyRaw.ToUpperInvariant();
        decimal? maxAmount = parameters.TryGetProperty("maxAmount", out var amt) && amt.ValueKind == JsonValueKind.Number
                             ? amt.GetDecimal() : null;

        if (string.IsNullOrWhiteSpace(delegateUser) || string.IsNullOrWhiteSpace(validFrom) || string.IsNullOrWhiteSpace(validTo))
            return FunctionResult.Fail("Please provide the delegate user, valid from, and valid to dates.");

        var delegateRole = await _scope.GetRoleBySapUserAsync(delegateUser, ct);
        if (delegateRole < UserRole.Manager)
        {
            return FunctionResult.Fail("Cannot delegate to an Employee. Delegation is only allowed for Managers or Admins.", "VALIDATION");
        }

        var fromDate = DateTimeOffset.Parse(validFrom);
        var toDate = DateTimeOffset.Parse(validTo);

        // Prevent chain delegation
        var delegatorInfo = await _scope.GetDelegationInfoAsync(requestingSapUser, ct);
        if (delegatorInfo.DelegatorSapUser != null)
        {
            return FunctionResult.Fail("Cannot delegate because you are currently acting as a delegate (Chain delegation is prohibited).", "VALIDATION");
        }

        var delegateeInfo = await _scope.GetDelegationInfoAsync(delegateUser, ct);
        if (delegateeInfo.DelegatorSapUser != null)
        {
            return FunctionResult.Fail($"Cannot delegate to {delegateUser} because they are currently acting as a delegate for someone else.", "VALIDATION");
        }

        return FunctionResult.Ok(new ConfirmDelegateApprovalResponse(
            delegateUser,
            validFrom,
            validTo,
            fromDate.ToString("dd/MM/yyyy"),
            toDate.ToString("dd/MM/yyyy"),
            reason ?? "None",
            maxAmount?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
            maxAmount.HasValue ? maxAmount.Value.ToString("N0") : "Unlimited",
            currency
        ));
    }
}

/// <summary>
/// Payload telling the bot to show <c>confirm-delegate</c> card.
/// </summary>
public sealed record ConfirmDelegateApprovalResponse(
    string DelegateUser,
    string ValidFromRaw,
    string ValidToRaw,
    string ValidFrom,
    string ValidTo,
    string Reason,
    string MaxAmountRaw,
    string MaxAmount,
    string Currency);
