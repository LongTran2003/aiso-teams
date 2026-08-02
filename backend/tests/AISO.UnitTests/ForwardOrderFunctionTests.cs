using System.Text.Json;
using AISO.AiOrchestration.Functions;
using AISO.Domain.Approvals;
using AISO.SapIntegration.Mock;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AISO.UnitTests;

public class ForwardOrderFunctionTests
{
    private sealed class FakeApprovals : IOrderApprovalService
    {
        public OrderApprovalRequest? Pending { get; set; }

        public Task<OrderApprovalRequest> RequestReleaseAsync(
            string soNumber, string requestedBySapUser, string? salesOrg, string? comment, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<OrderApprovalRequest?> GetPendingBySoNumberAsync(string soNumber, CancellationToken ct = default) =>
            Task.FromResult(Pending);

        public Task<IReadOnlyList<OrderApprovalRequest>> GetPendingAsync(string? salesOrgFilter, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<OrderApprovalRequest>>(Array.Empty<OrderApprovalRequest>());

        public Task<OrderApprovalRequest> ApproveAsync(
            string soNumber, string decidedBySapUser, string? managerSalesOrg, bool isAdmin, string? comment, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<OrderApprovalRequest> RejectAsync(
            string soNumber, string decidedBySapUser, string? managerSalesOrg, bool isAdmin, string? comment, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task Execute_WithoutRecipient_ReturnsConfirmPayload()
    {
        var sap = new MockSapClient();
        var fn = new ForwardOrderFunction(sap, new FakeApprovals(), NullLogger<ForwardOrderFunction>.Instance);

        using var doc = JsonDocument.Parse("""{"order_id":"0000005001"}""");
        var result = await fn.ExecuteAsync(doc.RootElement, "DEV-249");

        Assert.True(result.Success);
        var payload = Assert.IsType<ConfirmForwardResponse>(result.Payload);
        Assert.Equal("0000005001", payload.SoNumber);
        Assert.Null(payload.SuggestedRecipient);
        Assert.False(string.IsNullOrWhiteSpace(payload.SalesOrg));
    }

    [Fact]
    public async Task Execute_WithRecipient_ReturnsSuggestedRecipient_WithoutSapForward()
    {
        var sap = new MockSapClient();
        var fn = new ForwardOrderFunction(sap, new FakeApprovals(), NullLogger<ForwardOrderFunction>.Instance);

        using var doc = JsonDocument.Parse("""{"order_id":"0000005001","forward_to_user":"DEV-024"}""");
        var result = await fn.ExecuteAsync(doc.RootElement, "DEV-249");

        Assert.True(result.Success);
        var payload = Assert.IsType<ConfirmForwardResponse>(result.Payload);
        Assert.Equal("DEV-024", payload.SuggestedRecipient);
    }

    [Fact]
    public async Task Execute_WhenOrderMissing_Fails()
    {
        var sap = new MockSapClient();
        var fn = new ForwardOrderFunction(sap, new FakeApprovals(), NullLogger<ForwardOrderFunction>.Instance);

        using var doc = JsonDocument.Parse("""{"order_id":"9999999999"}""");
        var result = await fn.ExecuteAsync(doc.RootElement, "DEV-249");

        Assert.False(result.Success);
    }
}
