using AISO.Domain.Approvals;
using AISO.Persistence;
using AISO.Persistence.Approvals;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AISO.UnitTests;

public class OrderApprovalServiceTests
{
    private static OrderApprovalService CreateService(out AppDbContext ctx)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        ctx = new AppDbContext(options);
        var factory = new TestDbContextFactory(options);
        return new OrderApprovalService(factory);
    }

    [Fact]
    public async Task RequestRelease_CreatesPending()
    {
        var service = CreateService(out var ctx);
        await using (ctx)
        {
            var request = await service.RequestReleaseAsync("9", "DEV-249", "UE00", "please approve");

            Assert.Equal("0000000009", request.SoNumber);
            Assert.Equal(ApprovalStatus.Pending, request.Status);
            Assert.Equal("UE00", request.SalesOrg);
            Assert.Single(ctx.OrderApprovals);
        }
    }

    [Fact]
    public async Task RequestRelease_WhenAlreadyPending_Throws()
    {
        var service = CreateService(out var ctx);
        await using (ctx)
        {
            await service.RequestReleaseAsync("9", "DEV-249", "UE00", null);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.RequestReleaseAsync("9", "DEV-300", "UE00", null));
        }
    }

    [Fact]
    public async Task GetPendingBySoNumber_FindsWhenUnpaddedOrPadded()
    {
        var service = CreateService(out var ctx);
        await using (ctx)
        {
            await service.RequestReleaseAsync("13063", "DEV-249", "TV01", null);

            var byShort = await service.GetPendingBySoNumberAsync("13063");
            var byPadded = await service.GetPendingBySoNumberAsync("0000013063");

            Assert.NotNull(byShort);
            Assert.NotNull(byPadded);
            Assert.Equal("0000013063", byShort!.SoNumber);
            Assert.Equal(byShort.Id, byPadded!.Id);
        }
    }

    [Fact]
    public async Task RequestRelease_WhenAlreadyPending_DifferentPadding_Throws()
    {
        var service = CreateService(out var ctx);
        await using (ctx)
        {
            await service.RequestReleaseAsync("0000013063", "DEV-249", "TV01", null);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.RequestReleaseAsync("13063", "DEV-249", "TV01", null));
        }
    }

    [Fact]
    public async Task Approve_MarksApproved_WhenSameSalesOrg()
    {
        var service = CreateService(out var ctx);
        await using (ctx)
        {
            await service.RequestReleaseAsync("9", "DEV-249", "UE00", null);

            var approved = await service.ApproveAsync(
                "9", "MGR-1", "UE00", isAdmin: false, "ok");

            Assert.Equal(ApprovalStatus.Approved, approved.Status);
            Assert.Equal("MGR-1", approved.DecidedBySapUser);
        }
    }

    [Fact]
    public async Task Approve_WhenWrongSalesOrg_Throws()
    {
        var service = CreateService(out var ctx);
        await using (ctx)
        {
            await service.RequestReleaseAsync("9", "DEV-249", "UE00", null);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.ApproveAsync("9", "MGR-1", "DE00", isAdmin: false, null));
        }
    }

    [Fact]
    public async Task Approve_Admin_BypassesSalesOrg()
    {
        var service = CreateService(out var ctx);
        await using (ctx)
        {
            await service.RequestReleaseAsync("9", "DEV-249", "UE00", null);

            var approved = await service.ApproveAsync(
                "9", "ADMIN-1", "DE00", isAdmin: true, null);

            Assert.Equal(ApprovalStatus.Approved, approved.Status);
        }
    }

    [Fact]
    public async Task GetPending_FiltersBySalesOrg()
    {
        var service = CreateService(out var ctx);
        await using (ctx)
        {
            await service.RequestReleaseAsync("1", "A", "UE00", null);
            await service.RequestReleaseAsync("2", "B", "DE00", null);

            var ue00 = await service.GetPendingAsync("UE00");
            Assert.Single(ue00);
            Assert.Equal("0000000001", ue00[0].SoNumber);
        }
    }

    private sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDbContextFactory(DbContextOptions<AppDbContext> options) => _options = options;

        public AppDbContext CreateDbContext() => new(_options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new AppDbContext(_options));
    }
}
