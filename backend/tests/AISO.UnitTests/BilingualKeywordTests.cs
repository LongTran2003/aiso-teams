using AISO.AiOrchestration;
using AISO.AiOrchestration.Functions;
using AISO.AiOrchestration.Stub;
using AISO.Domain.Approvals;
using AISO.Domain.Users;
using AISO.SapIntegration;
using AISO.SapIntegration.Mock;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AISO.UnitTests;

public class BilingualKeywordTests
{
    [Theory]
    [InlineData("show pending approvals")]
    [InlineData("chờ duyệt")]
    [InlineData("danh sách chờ duyệt")]
    [InlineData("duyệt pending")]
    public async Task Keyword_PendingApprovals_EnAndVi(string message)
    {
        var dispatcher = CreateDispatcher();

        var result = await dispatcher.DispatchAsync(message, "DEV-MGR", UserRole.Manager);

        Assert.True(result.Handled);
        Assert.Equal("GetPendingApprovals", result.FunctionName);
    }

    [Theory]
    [InlineData("update reference 0000005001 to 'PO-99'")]
    [InlineData("cập nhật reference 0000005001 thành 'PO-99'")]
    public async Task Keyword_UpdateReference_EnAndVi(string message)
    {
        var dispatcher = CreateDispatcher();

        var result = await dispatcher.DispatchAsync(message, "DEV-249", UserRole.Employee);

        Assert.True(result.Handled);
        Assert.Equal("UpdateOrderReference", result.FunctionName);
        Assert.True(result.Result?.Success, result.Result?.ErrorMessage);
        var payload = Assert.IsType<ConfirmUpdateReferenceResponse>(result.Result!.Payload);
        Assert.Equal("0000005001", payload.SoNumber);
        Assert.Equal("PO-99", payload.NewReference, ignoreCase: true);
    }

    [Fact]
    public async Task Keyword_CreateOrder_ShowsConfirmForm()
    {
        var dispatcher = CreateDispatcher();

        var result = await dispatcher.DispatchAsync("create order", "DEV-249", UserRole.Employee);

        Assert.True(result.Handled);
        Assert.Equal("CreateOrder", result.FunctionName);
        Assert.True(result.Result?.Success);
        var payload = Assert.IsType<ConfirmCreateOrderResponse>(result.Result!.Payload);
        Assert.True(
            payload.Customer == "10100001"
            || payload.Customer.StartsWith("10100001|", StringComparison.OrdinalIgnoreCase),
            $"Unexpected customer form value: {payload.Customer}");
        Assert.Equal("TG11", payload.Material);
        Assert.Single(payload.Lines);
    }

    [Theory]
    [InlineData("edit order 0000005001")]
    [InlineData("sửa đơn 0000005001")]
    public async Task Keyword_EditOrder_EnAndVi(string message)
    {
        var dispatcher = CreateDispatcher();

        var result = await dispatcher.DispatchAsync(message, "DEV-249", UserRole.Employee);

        Assert.True(result.Handled);
        Assert.Equal("EditOrder", result.FunctionName);
        Assert.True(result.Result?.Success, result.Result?.ErrorMessage);
        var payload = Assert.IsType<ConfirmEditOrderResponse>(result.Result!.Payload);
        Assert.Equal("0000005001", payload.SoNumber);
    }

    [Fact]
    public void BuildConfirmCreateAndUpdateCards_IncludeActions()
    {
        var create = AISO.Bot.Cards.Builders.TeamsCardBuilder.BuildConfirmCreateOrderCard(
            "10100001",
            "1010",
            "USD",
            lines: new[]
            {
                new ConfirmCreateOrderLine("TG11", 2),
                new ConfirmCreateOrderLine("DXTR1000", 5)
            });
        var createJson = Newtonsoft.Json.JsonConvert.SerializeObject(create.Content);
        Assert.Contains("create_so_confirm", createJson);
        Assert.Contains("10100001", createJson);
        Assert.Contains("material1", createJson);
        Assert.Contains("TG11", createJson);
        Assert.Contains("DXTR1000", createJson);
        Assert.Contains("ColumnSet", createJson);
        Assert.DoesNotContain("salesOrgCustom", createJson);
        Assert.Contains("Input.ChoiceSet", createJson);
        Assert.Contains("salesArea", createJson);

        var update = AISO.Bot.Cards.Builders.TeamsCardBuilder.BuildConfirmUpdateReferenceCard(
            "0000005001", "OLD", "NEW-PO");
        var updateJson = Newtonsoft.Json.JsonConvert.SerializeObject(update.Content);
        Assert.Contains("update_ref_confirm", updateJson);
        Assert.Contains("NEW-PO", updateJson);

        var edit = AISO.Bot.Cards.Builders.TeamsCardBuilder.BuildConfirmEditOrderCard(
            "0000005001", "OLD", "NEW", "2026-08-01", "2026-08-15",
            "U", "10", "TG11", 2, "1010", "PC", "10 · TG11 x 2 PC");
        var editJson = Newtonsoft.Json.JsonConvert.SerializeObject(edit.Content);
        Assert.Contains("edit_so_confirm", editJson);
        Assert.Contains("Edit sales order", editJson);
    }

    [Theory]
    [InlineData("phê duyệt đơn 0000005001")]
    [InlineData("duyệt đơn 0000005001")]
    [InlineData("approve order 0000005001")]
    public async Task Keyword_Approve_EnAndVi(string message)
    {
        var dispatcher = CreateDispatcher();

        var result = await dispatcher.DispatchAsync(message, "DEV-MGR", UserRole.Manager);

        Assert.True(result.Handled);
        Assert.Equal("ReleaseOrder", result.FunctionName);
    }

    [Theory]
    [InlineData("từ chối duyệt 0000005001")]
    [InlineData("không duyệt 0000005001")]
    [InlineData("reject approval 0000005001")]
    public async Task Keyword_RejectApproval_EnAndVi(string message)
    {
        var dispatcher = CreateDispatcher();

        var result = await dispatcher.DispatchAsync(message, "DEV-MGR", UserRole.Manager);

        Assert.True(result.Handled);
        Assert.Equal("RejectApproval", result.FunctionName);
    }

    [Theory]
    [InlineData("yêu cầu duyệt 0000005001")]
    [InlineData("xin duyệt 0000005001")]
    [InlineData("request release 0000005001")]
    public async Task Keyword_RequestRelease_EnAndVi(string message)
    {
        var dispatcher = CreateDispatcher();

        var result = await dispatcher.DispatchAsync(message, "DEV-249", UserRole.Employee);

        Assert.True(result.Handled);
        Assert.Equal("RequestRelease", result.FunctionName);
    }

    [Theory]
    [InlineData("show my sales orders")]
    [InlineData("my orders")]
    [InlineData("đơn của tôi")]
    [InlineData("đơn hàng của tôi")]
    public async Task Keyword_MyOrders_SetsOwnedByMe(string message)
    {
        var dispatcher = CreateDispatcher();

        var result = await dispatcher.DispatchAsync(message, "DEV-249", UserRole.Employee);

        Assert.True(result.Handled);
        Assert.Equal("GetSalesOrders", result.FunctionName);
        Assert.True(result.Result?.Success, result.Result?.ErrorMessage);
        Assert.Contains("ownedByMe", result.ParametersJson ?? "", StringComparison.OrdinalIgnoreCase);
        var payload = Assert.IsType<GetSalesOrdersResponse>(result.Result!.Payload);
        Assert.Equal("My sales orders", payload.Title);
        Assert.All(payload.Orders, o => Assert.Equal("DEV-249", o.OwnerSapUser));
    }

    [Theory]
    [InlineData("show overdue orders")]
    [InlineData("xem đơn hàng quá hạn")]
    [InlineData("đơn giao trễ")]
    public async Task Keyword_OverdueOrders_RoutesToGetOverdueOrders(string message)
    {
        var dispatcher = CreateDispatcher();

        var result = await dispatcher.DispatchAsync(message, "DEV-249", UserRole.Employee);

        Assert.True(result.Handled);
        Assert.Equal("GetOverdueOrders", result.FunctionName);
        Assert.True(result.Result?.Success, result.Result?.ErrorMessage);
        Assert.IsType<GetOverdueOrdersResponse>(result.Result!.Payload);
    }

    [Fact]
    public void IsDeterministicShortcut_IncludesPendingAndUpdateReference()
    {
        Assert.True(AiServiceDispatcher.IsDeterministicShortcut("show pending approvals"));
        Assert.True(AiServiceDispatcher.IsDeterministicShortcut("chờ duyệt"));
        Assert.True(AiServiceDispatcher.IsDeterministicShortcut("update reference 13122"));
        Assert.True(AiServiceDispatcher.IsDeterministicShortcut("edit order 13122"));
        Assert.True(AiServiceDispatcher.IsDeterministicShortcut("sửa đơn 13122"));
        Assert.True(AiServiceDispatcher.IsDeterministicShortcut("từ chối duyệt 13122"));
        Assert.True(AiServiceDispatcher.IsDeterministicShortcut("show my sales orders"));
        Assert.True(AiServiceDispatcher.IsDeterministicShortcut("đơn của tôi"));
        Assert.True(AiServiceDispatcher.IsDeterministicShortcut("show overdue orders"));
        Assert.True(AiServiceDispatcher.IsDeterministicShortcut("quá hạn"));
    }

    private static KeywordFunctionDispatcher CreateDispatcher()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISapClient, MockSapClient>();
        services.AddLogging();
        services.AddSingleton<IOrderApprovalService, NoopApprovals>();
        services.AddSingleton<IUserScopeLookup, NoopScope>();
        services.AddSingleton<IFunction, GetPendingApprovalsFunction>();
        services.AddSingleton<IFunction, UpdateOrderReferenceFunction>();
        services.AddSingleton<IFunction, CreateOrderFunction>();
        services.AddSingleton<IFunction, EditOrderFunction>();
        services.AddSingleton<IFunction, ReleaseOrderFunction>();
        services.AddSingleton<IFunction, RejectApprovalFunction>();
        services.AddSingleton<IFunction, RequestReleaseFunction>();
        services.AddSingleton<IFunction, GetSalesOrdersFunction>();
        services.AddSingleton<IFunction, GetOverdueOrdersFunction>();
        services.AddSingleton<IFunctionRegistry, FunctionRegistry>();
        var sp = services.BuildServiceProvider();
        return new KeywordFunctionDispatcher(sp.GetRequiredService<IFunctionRegistry>());
    }

    private sealed class NoopApprovals : IOrderApprovalService
    {
        public Task<OrderApprovalRequest> RequestReleaseAsync(
            string soNumber, string requestedBySapUser, string? salesOrg, string? comment, CancellationToken ct = default)
            => Task.FromResult(new OrderApprovalRequest
            {
                Id = Guid.NewGuid(),
                SoNumber = soNumber,
                RequestedBySapUser = requestedBySapUser,
                Status = ApprovalStatus.Pending,
                RequestedAt = DateTimeOffset.UtcNow,
                Comment = comment
            });

        public Task<OrderApprovalRequest?> GetPendingBySoNumberAsync(string soNumber, CancellationToken ct = default)
            => Task.FromResult<OrderApprovalRequest?>(null);

        public Task<OrderApprovalRequest?> GetLatestBySoNumberAsync(string soNumber, CancellationToken ct = default)
            => Task.FromResult<OrderApprovalRequest?>(null);

        public Task<IReadOnlyList<OrderApprovalRequest>> GetPendingAsync(string? salesOrgFilter, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<OrderApprovalRequest>>(Array.Empty<OrderApprovalRequest>());

        public Task<OrderApprovalRequest> ApproveAsync(
            string soNumber, string decidedBySapUser, string? managerSalesOrg, bool isAdmin, string? comment, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<OrderApprovalRequest> RejectAsync(
            string soNumber, string decidedBySapUser, string? managerSalesOrg, bool isAdmin, string? comment, CancellationToken ct = default)
            => Task.FromResult(new OrderApprovalRequest
            {
                Id = Guid.NewGuid(),
                SoNumber = soNumber,
                RequestedBySapUser = "DEV-024",
                Status = ApprovalStatus.Rejected,
                DecidedBySapUser = decidedBySapUser,
                RequestedAt = DateTimeOffset.UtcNow,
                DecidedAt = DateTimeOffset.UtcNow
            });
    }

    private sealed class NoopScope : IUserScopeLookup
    {
        public Task<UserRole> GetRoleBySapUserAsync(string sapUserId, CancellationToken ct = default)
            => Task.FromResult(
                sapUserId.Contains("MGR", StringComparison.OrdinalIgnoreCase)
                    ? UserRole.Manager
                    : UserRole.Employee);

        public Task<string?> GetSalesOrgBySapUserAsync(string sapUserId, CancellationToken ct = default) =>
            Task.FromResult<string?>("UE00");

        public Task<string?> GetDelegatedBySapUserAsync(string sapUserId, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> GetEmailBySapUserAsync(string sapUserId, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);

        public Task<AISO.Domain.Users.DelegationInfo> GetDelegationInfoAsync(string sapUserId, CancellationToken ct = default) =>
            Task.FromResult(new AISO.Domain.Users.DelegationInfo(null, null));

        public Task SetDelegatedBySapUserAsync(string delegateUser, string? delegatorUser, DateTimeOffset? validTo = null, decimal? maxAmount = null, CancellationToken ct = default)
            => Task.CompletedTask; public Task<IReadOnlyList<AISO.Domain.Users.ActiveDelegation>> GetActiveDelegationsAsync(string? filterDelegatorUser = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<AISO.Domain.Users.ActiveDelegation>>(Array.Empty<AISO.Domain.Users.ActiveDelegation>());
    }
}
