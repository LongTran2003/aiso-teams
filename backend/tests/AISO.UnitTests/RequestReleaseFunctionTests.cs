using System.Text.Json;
using AISO.AiOrchestration.Functions;
using AISO.Domain.Approvals;
using AISO.SapIntegration.Mock;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AISO.UnitTests;

public class RequestReleaseFunctionTests
{
    private sealed class FakeApprovals : IOrderApprovalService
    {
        public OrderApprovalRequest? Pending { get; set; }
        public int RequestReleaseCallCount { get; private set; }

        public Task<OrderApprovalRequest> RequestReleaseAsync(
            string soNumber,
            string requestedBySapUser,
            string? salesOrg,
            string? comment,
            CancellationToken ct = default)
        {
            RequestReleaseCallCount++;
            return Task.FromResult(MakePending(soNumber, requestedBySapUser, salesOrg, comment));
        }

        public Task<OrderApprovalRequest?> GetPendingBySoNumberAsync(string soNumber, CancellationToken ct = default) =>
            Task.FromResult(Pending);

        public Task<OrderApprovalRequest?> GetLatestBySoNumberAsync(string soNumber, CancellationToken ct = default) =>
            Task.FromResult(Pending);

        public Task<IReadOnlyList<OrderApprovalRequest>> GetPendingAsync(string? salesOrgFilter, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<OrderApprovalRequest>>(Array.Empty<OrderApprovalRequest>());

        public Task<OrderApprovalRequest> ApproveAsync(
            string soNumber,
            string decidedBySapUser,
            string? managerSalesOrg,
            bool isAdmin,
            string? comment,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<OrderApprovalRequest> RejectAsync(
            string soNumber,
            string decidedBySapUser,
            string? managerSalesOrg,
            bool isAdmin,
            string? comment,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public static OrderApprovalRequest MakePending(
            string soNumber,
            string requestedBy,
            string? salesOrg = null,
            string? comment = null) =>
            new()
            {
                Id = Guid.NewGuid(),
                SoNumber = soNumber,
                RequestedBySapUser = requestedBy,
                SalesOrg = salesOrg,
                Comment = comment,
                Status = ApprovalStatus.Pending,
                RequestedAt = DateTimeOffset.UtcNow
            };
    }

    [Fact]
    public async Task Execute_ReturnsConfirmPayload_WithoutCreatingApproval()
    {
        var sap = new MockSapClient();
        var approvals = new FakeApprovals();
        var fn = new RequestReleaseFunction(sap, approvals, NullLogger<RequestReleaseFunction>.Instance);

        using var doc = JsonDocument.Parse("""{"order_id":"0000005001","comment":"pls approve"}""");
        var result = await fn.ExecuteAsync(doc.RootElement, "DEV-249");

        Assert.True(result.Success);
        var payload = Assert.IsType<ConfirmRequestReleaseResponse>(result.Payload);
        Assert.Equal("0000005001", payload.SoNumber);
        Assert.Equal("pls approve", payload.Comment);
        Assert.Equal(0, approvals.RequestReleaseCallCount);
    }

    [Fact]
    public async Task Execute_WhenAlreadyPending_FailsWithoutSubmit()
    {
        var sap = new MockSapClient();
        var approvals = new FakeApprovals
        {
            Pending = FakeApprovals.MakePending("0000005001", "DEV-100")
        };
        var fn = new RequestReleaseFunction(sap, approvals, NullLogger<RequestReleaseFunction>.Instance);

        using var doc = JsonDocument.Parse("""{"order_id":"0000005001"}""");
        var result = await fn.ExecuteAsync(doc.RootElement, "DEV-249");

        Assert.False(result.Success);
        Assert.Equal(0, approvals.RequestReleaseCallCount);
        Assert.Contains("pending", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_WhenOrderMissing_Fails()
    {
        var sap = new MockSapClient();
        var approvals = new FakeApprovals();
        var fn = new RequestReleaseFunction(sap, approvals, NullLogger<RequestReleaseFunction>.Instance);

        using var doc = JsonDocument.Parse("""{"order_id":"9999999999"}""");
        var result = await fn.ExecuteAsync(doc.RootElement, "DEV-249");

        Assert.False(result.Success);
        Assert.Equal(0, approvals.RequestReleaseCallCount);
    }
}
