using System.Text.Json;
using System.Text.Json.Serialization;

using AISO.Domain.Approvals;
using AISO.Domain.Users;
using AISO.Domain.Notifications;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

public class ForceDelegateApprovalFunction : IFunction
{
    public string Name => "ForceDelegateApproval";
    public string Description => "Emergency override: forces a delegation on behalf of another user.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "delegatorUser": {
              "type": "string",
              "description": "SAP User ID of the manager whose rights are being delegated."
            },
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
              "description": "Reason for emergency delegation."
            }
          },
          "required": ["delegatorUser", "delegateUser", "validFrom", "validTo"]
        }
        """;

    public UserRole MinimumRole => UserRole.Admin;

    private readonly ISapClient _sapClient;
    private readonly IUserScopeLookup _scope;
    private readonly ILogger<ForceDelegateApprovalFunction> _logger;
    private readonly IEmailService _emailService;

    public ForceDelegateApprovalFunction(ISapClient sapClient, IUserScopeLookup scope, ILogger<ForceDelegateApprovalFunction> logger, IEmailService emailService)
    {
        _sapClient = sapClient;
        _scope = scope;
        _logger = logger;
        _emailService = emailService;
    }

    public async Task<FunctionResult> ExecuteAsync(
        JsonElement parameters,
        string requestingSapUser,
        CancellationToken ct)
    {
        var delegatorUser = parameters.TryGetProperty("delegatorUser", out var dOrg) ? dOrg.GetString() : null;
        var delegateUser = parameters.TryGetProperty("delegateUser", out var dUser) ? dUser.GetString() : null;
        var validFrom = parameters.TryGetProperty("validFrom", out var vFrom) ? vFrom.GetString() : null;
        var validTo = parameters.TryGetProperty("validTo", out var vTo) ? vTo.GetString() : null;
        var reason = parameters.TryGetProperty("reason", out var r) ? r.GetString() : null;
        decimal? maxAmount = parameters.TryGetProperty("maxAmount", out var amt) && amt.ValueKind == JsonValueKind.Number
                             ? amt.GetDecimal() : null;

        if (string.IsNullOrWhiteSpace(delegatorUser) || string.IsNullOrWhiteSpace(delegateUser) || string.IsNullOrWhiteSpace(validFrom) || string.IsNullOrWhiteSpace(validTo))
            return FunctionResult.Fail("Vui lòng cung cấp đủ thông tin người uỷ quyền, người nhận và thời hạn.");

        var fromDate = DateTimeOffset.Parse(validFrom);
        var toDate = DateTimeOffset.Parse(validTo);
        var salesOrg = await _scope.GetSalesOrgBySapUserAsync(delegatorUser, ct);

        var dto = new DelegateApprovalDto(
            RequestingTeamsUser: delegatorUser, // Pretend to be the delegator
            DelegateUser: delegateUser,
            SalesOrg: salesOrg,
            ValidFrom: fromDate,
            ValidTo: toDate,
            Reason: reason ?? $"Force delegated by Admin {requestingSapUser}",
            MaxAmount: maxAmount);

        try
        {
            _logger.LogWarning("EMERGENCY OVERRIDE: Admin {Admin} is forcing delegation from {Delegator} to {Delegatee}", requestingSapUser, delegatorUser, delegateUser);
            await _sapClient.DelegateApprovalAsync(dto, ct);

            // Cập nhật local DB
            await _scope.SetDelegatedBySapUserAsync(delegateUser, delegatorUser, toDate, maxAmount, ct);

            // Send Email Notification
            var delegateEmail = await _scope.GetEmailBySapUserAsync(delegateUser, ct);
            if (!string.IsNullOrEmpty(delegateEmail))
            {
                string subject = $"[Cưỡng Chế] Thông báo nhận uỷ quyền phê duyệt từ hệ thống";
                string html = $@"
                    <h2>Thông báo uỷ quyền khẩn cấp (Emergency Delegation)</h2>
                    <p>Chào bạn,</p>
                    <p>Hệ thống vừa tự động chuyển giao quyền phê duyệt của <b>{delegatorUser}</b> sang cho bạn. Thao tác này được thực hiện bởi Admin <b>{requestingSapUser}</b>.</p>
                    <ul>
                        <li><b>Thời gian bắt đầu:</b> {fromDate:dd/MM/yyyy}</li>
                        <li><b>Thời gian kết thúc:</b> {toDate:dd/MM/yyyy}</li>
                        <li><b>Hạn mức tối đa:</b> {(maxAmount.HasValue ? maxAmount.Value.ToString("N0") : "Không giới hạn")}</li>
                        <li><b>Lý do Admin ghi chú:</b> {reason ?? "Không có"}</li>
                    </ul>
                    <p>Vui lòng đăng nhập vào ứng dụng AISO Teams Bot để xử lý các yêu cầu phê duyệt trong thời gian này.</p>
                ";
                await _emailService.SendEmailAsync(delegateEmail, subject, html, ct);
            }

            return FunctionResult.Ok(new
            {
                action = "ForceDelegated",
                delegatorUser,
                delegateUser,
                maxAmount,
                message = $"Đã thực hiện uỷ quyền khẩn cấp quyền của {delegatorUser} sang cho {delegateUser} từ {fromDate:dd/MM/yyyy} đến {toDate:dd/MM/yyyy}." + (maxAmount.HasValue ? $" Hạn mức: {maxAmount.Value:N0}" : "")
            });
        }
        catch (SapODataException ex)
        {
            return FunctionResult.Fail($"SAP từ chối uỷ quyền: {ex.Message}", "VALIDATION");
        }
        catch (Exception ex)
        {
            return FunctionResult.Fail($"Lỗi khi uỷ quyền khẩn cấp: {ex.Message}");
        }
    }
}
