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

        var salesOrg = await _scope.GetSalesOrgBySapUserAsync(requestingSapUser, ct);

        var dto = new DelegateApprovalDto(
            RequestingTeamsUser: requestingSapUser,
            DelegateUser: delegateUser,
            SalesOrg: salesOrg,
            ValidFrom: fromDate,
            ValidTo: toDate,
            Reason: reason,
            MaxAmount: maxAmount);

        try
        {
            await _sapClient.DelegateApprovalAsync(dto, ct);

            // Cập nhật local DB
            // Update local DB
            await _scope.SetDelegatedBySapUserAsync(delegateUser, requestingSapUser, toDate, maxAmount, ct);

            // Send Email Notification
            var delegateEmail = await _scope.GetEmailBySapUserAsync(delegateUser, ct);
            if (!string.IsNullOrEmpty(delegateEmail))
            {
                string subject = $"Delegation Notice from {requestingSapUser}";
                string html = $@"
                    <h2>Delegation Notice</h2>
                    <p>You have been delegated by <b>{requestingSapUser}</b> to approve SAP orders (Sales Org: {salesOrg ?? "All"}).</p>
                    <ul>
                        <li><b>Start Date:</b> {fromDate:dd/MM/yyyy}</li>
                        <li><b>End Date:</b> {toDate:dd/MM/yyyy}</li>
                        <li><b>Max Amount:</b> {(maxAmount.HasValue ? $"{maxAmount.Value:N0} {dto.Currency}" : "Unlimited")}</li>
                        <li><b>Reason:</b> {reason ?? "None"}</li>
                    </ul>
                    <p>Please log in to the AISO Teams Bot to process approval requests during this period.</p>
                ";
                await _emailService.SendEmailAsync(delegateEmail, subject, html, ct);
            }

            return FunctionResult.Ok(new
            {
                action = "Delegated",
                delegateUser,
                maxAmount,
                message = $"Successfully delegated to {delegateUser} from {fromDate:dd/MM/yyyy} to {toDate:dd/MM/yyyy}." + (maxAmount.HasValue ? $" Max Amount: {maxAmount.Value:N0} {dto.Currency}" : "")
            });
        }
        catch (SapODataException ex)
        {
            return FunctionResult.Fail($"SAP rejected delegation: {ex.Message}", "VALIDATION");
        }
        catch (Exception ex)
        {
            return FunctionResult.Fail($"Error delegating: {ex.Message}");
        }
    }
}
