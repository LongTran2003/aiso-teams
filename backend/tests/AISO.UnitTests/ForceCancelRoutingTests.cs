using System.Text.Json;
using AISO.AiOrchestration;
using AISO.AiOrchestration.Functions;
using AISO.AiOrchestration.Stub;
using AISO.Domain.Approvals;
using AISO.Domain.SalesOrders;
using AISO.Domain.Users;
using AISO.SapIntegration;
using AISO.SapIntegration.Mock;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AISO.UnitTests;

public class ForceCancelRoutingTests
{
    [Fact]
    public async Task Keyword_ForceCancelWithoutOrderWord_RoutesToForceCancel()
    {
        var dispatcher = CreateKeywordDispatcher();

        var result = await dispatcher.DispatchAsync(
            "force cancel 0000013069",
            "DEV-001",
            UserRole.Admin);

        Assert.True(result.Handled);
        Assert.False(result.Denied);
        Assert.Equal("ForceCancel", result.FunctionName);
        Assert.Contains("0000013069", result.ParametersJson ?? "", StringComparison.Ordinal);
        Assert.Contains("reason", result.ParametersJson ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Keyword_ForceRelease_RoutesToForceRelease()
    {
        var dispatcher = CreateKeywordDispatcher();

        var result = await dispatcher.DispatchAsync(
            "force release order 13122 reason: unlock now",
            "DEV-001",
            UserRole.Admin);

        Assert.True(result.Handled);
        Assert.Equal("ForceRelease", result.FunctionName);
        Assert.Contains("unlock now", result.ParametersJson ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keyword_RejectApproval_DoesNotRouteToRejectOrder()
    {
        var dispatcher = CreateKeywordDispatcher();

        var result = await dispatcher.DispatchAsync(
            "Reject approval 0000013069",
            "DEV-024",
            UserRole.Manager);

        Assert.True(result.Handled);
        Assert.Equal("RejectApproval", result.FunctionName);
    }

    [Fact]
    public async Task Keyword_PlainCancelOrder_StillRoutesToRejectOrder()
    {
        var dispatcher = CreateKeywordDispatcher();

        var result = await dispatcher.DispatchAsync(
            "reject order 0000000009 reason: customer cancel",
            "DEV-249",
            UserRole.Employee);

        Assert.True(result.Handled);
        Assert.Equal("RejectOrder", result.FunctionName);
    }

    [Fact]
    public void IsDeterministicShortcut_IncludesForceCancel()
    {
        Assert.True(AiServiceDispatcher.IsDeterministicShortcut("force cancel 0000013069"));
        Assert.True(AiServiceDispatcher.IsDeterministicShortcut("force release 13122"));
        Assert.True(AiServiceDispatcher.IsDeterministicShortcut("reject approval 9"));
    }

    [Fact]
    public void EnsureForceReasonArg_AddsReasonFromReasonCode()
    {
        var json = AiServiceDispatcher.EnsureForceReasonArg(
            """{"order_id":"0000013069","reason_code":"OTHER"}""",
            "Admin force cancel via Teams");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("OTHER", doc.RootElement.GetProperty("reason").GetString());
        Assert.Equal("0000013069", doc.RootElement.GetProperty("order_id").GetString());
    }

    [Fact]
    public async Task ForceCancel_WhenDelivered_UsesForceCancelWording()
    {
        var sap = new StatusFixedSapClient(SalesOrderStatus.Delivered);
        var fn = new ForceCancelFunction(sap, NullLogger<ForceCancelFunction>.Instance);

        using var doc = JsonDocument.Parse("""{"order_id":"0000013069","reason":"test"}""");
        var result = await fn.ExecuteAsync(doc.RootElement, "DEV-001");

        Assert.False(result.Success);
        Assert.Equal("VALIDATION", result.ErrorCode);
        Assert.Contains("Force cancel", result.ErrorMessage ?? "", StringComparison.Ordinal);
        Assert.Contains("Delivered", result.ErrorMessage ?? "", StringComparison.Ordinal);
        Assert.DoesNotContain("Reject is not allowed", result.ErrorMessage ?? "", StringComparison.Ordinal);
    }

    private static KeywordFunctionDispatcher CreateKeywordDispatcher()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISapClient, MockSapClient>();
        services.AddLogging();
        services.AddSingleton<IOrderApprovalService, NoopApprovals>();
        services.AddSingleton<IUserScopeLookup, NoopScope>();
        services.AddSingleton<IFunction, ForceCancelFunction>();
        services.AddSingleton<IFunction, ForceReleaseFunction>();
        services.AddSingleton<IFunction, RejectApprovalFunction>();
        services.AddSingleton<IFunction, RejectOrderFunction>();
        services.AddSingleton<IFunctionRegistry, FunctionRegistry>();
        var sp = services.BuildServiceProvider();
        return new KeywordFunctionDispatcher(sp.GetRequiredService<IFunctionRegistry>());
    }

    /// <summary>Minimal SAP stub that returns a fixed status for get-by-id.</summary>
    private sealed class StatusFixedSapClient : ISapClient
    {
        private readonly SalesOrderStatus _status;

        public StatusFixedSapClient(SalesOrderStatus status) => _status = status;

        public Task<SalesOrder?> GetSalesOrderByIdAsync(string soNumber, CancellationToken ct = default) =>
            Task.FromResult<SalesOrder?>(new SalesOrder
            {
                SoNumber = soNumber.PadLeft(10, '0'),
                CustomerId = "1000",
                CustomerName = "Acme",
                SalesOrg = "FU24",
                OrderDate = new DateOnly(2026, 8, 1),
                NetValue = 480000,
                Currency = "VND",
                Status = _status,
                Items = Array.Empty<SalesOrderItem>()
            });

        public Task<IReadOnlyList<SalesOrder>> GetSalesOrdersAsync(SalesOrdersQuery query, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<SalesOrder> CreateSalesOrderAsync(CreateSalesOrderDto dto, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task SyncUserRoleAsync(string targetSapUser, string newRole, string? salesOrg, string requestingAdminSapUser, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<SalesOrder> UpdateReferenceAsync(string soNumber, string newReference, string requestingSapUser, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<SalesOrder> RejectOrderAsync(string soNumber, string rejectionCode, string requestingTeamsUser, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<SalesOrder> ReleaseOrderAsync(string soNumber, string requestingTeamsUser, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<SalesOrder> ApproveOrderAsync(string soNumber, string requestingSapUser, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<SalesOrder> RejectApprovalAsync(string soNumber, string requestingSapUser, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<SalesOrder> ForceReleaseAsync(string soNumber, string requestingSapUser, string overrideReason, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<SalesOrder> ForceCancelAsync(string soNumber, string requestingSapUser, string overrideReason, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<SalesOrder> ReassignOwnerAsync(string soNumber, string newOwnerSapUser, string requestingSapUser, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<SalesOrder> ForwardOrderAsync(string soNumber, string forwardToUser, string requestingTeamsUser, CancellationToken ct = default, string? remarks = null)
            => throw new NotImplementedException();
        public Task<AISO.Domain.Kpi.KpiSummary> GetKpiSummaryAsync(AISO.Domain.Kpi.KpiSummaryQuery query, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<IReadOnlyList<AISO.Domain.Kpi.KpiByCustomer>> GetKpiByCustomerAsync(AISO.Domain.Kpi.KpiByCustomerQuery query, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<IReadOnlyList<AISO.Domain.Kpi.KpiByProduct>> GetKpiByProductAsync(AISO.Domain.Kpi.KpiByProductQuery query, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<IReadOnlyList<AISO.Domain.Kpi.OverdueOrder>> GetOverdueOrdersAsync(AISO.Domain.Kpi.OverdueOrdersQuery query, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<bool?> SapUserExistsAsync(string sapUserId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private sealed class NoopApprovals : IOrderApprovalService
    {
        public Task<OrderApprovalRequest> RequestReleaseAsync(
            string soNumber, string requestedBySapUser, string? salesOrg, string? comment, CancellationToken ct = default)
            => throw new NotImplementedException();

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
            => Task.FromResult(UserRole.Manager);

        public Task<string?> GetSalesOrgBySapUserAsync(string sapUserId, CancellationToken ct = default)
            => Task.FromResult<string?>("FU24");
    }
}
