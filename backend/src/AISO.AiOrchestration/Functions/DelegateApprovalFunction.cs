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
            return FunctionResult.Fail("Vui lòng cung cấp đủ thông tin người được uỷ quyền và thời hạn.");

        var delegateRole = await _scope.GetRoleBySapUserAsync(delegateUser, ct);
        if (delegateRole < UserRole.Manager)
        {
            return FunctionResult.Fail("Không thể uỷ quyền cho nhân viên (Employee). Chỉ uỷ quyền được cho Manager hoặc Admin.", "VALIDATION");
        }

        var fromDate = DateTimeOffset.Parse(validFrom);
        var toDate = DateTimeOffset.Parse(validTo);

        // Chống uỷ quyền bắc cầu (No chain delegation)
        var delegatorInfo = await _scope.GetDelegationInfoAsync(requestingSapUser, ct);
        if (delegatorInfo.DelegatorSapUser != null)
        {
            return FunctionResult.Fail("Không thể uỷ quyền vì bạn đang nhận uỷ quyền từ người khác (Cấm uỷ quyền bắc cầu).", "VALIDATION");
        }

        var delegateeInfo = await _scope.GetDelegationInfoAsync(delegateUser, ct);
        if (delegateeInfo.DelegatorSapUser != null)
        {
            return FunctionResult.Fail($"Không thể uỷ quyền cho {delegateUser} vì họ đang nhận uỷ quyền của người khác.", "VALIDATION");
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
            await _scope.SetDelegatedBySapUserAsync(delegateUser, requestingSapUser, toDate, maxAmount, ct);

            // Send Email Notification
            var delegateEmail = await _scope.GetEmailBySapUserAsync(delegateUser, ct);
            if (!string.IsNullOrEmpty(delegateEmail))
            {
                string subject = $"Thông báo uỷ quyền phê duyệt từ {requestingSapUser}";
                string html = $@"
                    <h2>Thông báo uỷ quyền phê duyệt</h2>
                    <p>Chào bạn,</p>
                    <p>Bạn vừa được <b>{requestingSapUser}</b> uỷ quyền phê duyệt các đơn hàng SAP (Sales Org: {salesOrg ?? "All"}).</p>
                    <ul>
                        <li><b>Thời gian bắt đầu:</b> {fromDate:dd/MM/yyyy}</li>
                        <li><b>Thời gian kết thúc:</b> {toDate:dd/MM/yyyy}</li>
                        <li><b>Hạn mức tối đa:</b> {(maxAmount.HasValue ? maxAmount.Value.ToString("N0") : "Không giới hạn")}</li>
                        <li><b>Lý do:</b> {reason ?? "Không có"}</li>
                    </ul>
                    <p>Vui lòng đăng nhập vào ứng dụng AISO Teams Bot để xử lý các yêu cầu phê duyệt trong thời gian này.</p>
                ";
                await _emailService.SendEmailAsync(delegateEmail, subject, html, ct);
            }

            return FunctionResult.Ok(new
            {
                action = "Delegated",
                delegateUser,
                maxAmount,
                message = $"Đã uỷ quyền thành công cho {delegateUser} từ {fromDate:dd/MM/yyyy} đến {toDate:dd/MM/yyyy}." + (maxAmount.HasValue ? $" Hạn mức: {maxAmount.Value:N0}" : "")
            });
        }
        catch (SapODataException ex)
        {
            return FunctionResult.Fail($"SAP từ chối uỷ quyền: {ex.Message}", "VALIDATION");
        }
        catch (Exception ex)
        {
            return FunctionResult.Fail($"Lỗi khi uỷ quyền: {ex.Message}");
        }
    }
}
