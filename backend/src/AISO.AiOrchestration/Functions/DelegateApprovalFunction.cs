using System.Text.Json;
using System.Text.Json.Serialization;

using AISO.Domain.Approvals;
using AISO.Domain.Users;
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

    public DelegateApprovalFunction(ISapClient sapClient, IUserScopeLookup scope)
    {
        _sapClient = sapClient;
        _scope = scope;
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

        if (string.IsNullOrWhiteSpace(delegateUser) || string.IsNullOrWhiteSpace(validFrom) || string.IsNullOrWhiteSpace(validTo))
            return FunctionResult.Fail("Vui lòng cung cấp đủ thông tin người được ủy quyền và thời hạn.");

        var delegateRole = await _scope.GetRoleBySapUserAsync(delegateUser, ct);
        if (delegateRole < UserRole.Manager)
        {
            return FunctionResult.Fail("Không thể uỷ quyền cho nhân viên (Employee). Chỉ uỷ quyền được cho Manager hoặc Admin.", "VALIDATION");
        }

        var fromDate = DateTimeOffset.Parse(validFrom);
        var toDate = DateTimeOffset.Parse(validTo);
        var salesOrg = await _scope.GetSalesOrgBySapUserAsync(requestingSapUser, ct);

        var dto = new DelegateApprovalDto(
            RequestingTeamsUser: requestingSapUser,
            DelegateUser: delegateUser,
            SalesOrg: salesOrg,
            ValidFrom: fromDate,
            ValidTo: toDate,
            Reason: reason);

        try
        {
            await _sapClient.DelegateApprovalAsync(dto, ct);

            // Cập nhật local DB
            await _scope.SetDelegatedBySapUserAsync(delegateUser, requestingSapUser, ct);

            return FunctionResult.Ok(new
            {
                action = "Delegated",
                delegateUser,
                message = $"Đã ủy quyền thành công cho {delegateUser} từ {fromDate:dd/MM/yyyy} đến {toDate:dd/MM/yyyy}."
            });
        }
        catch (SapODataException ex)
        {
            return FunctionResult.Fail($"SAP từ chối uỷ quyền: {ex.Message}", "VALIDATION");
        }
        catch (Exception ex)
        {
            return FunctionResult.Fail($"Lỗi khi ủy quyền: {ex.Message}");
        }
    }
}
