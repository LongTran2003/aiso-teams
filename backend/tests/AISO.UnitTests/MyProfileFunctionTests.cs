using System.Text.Json;
using AISO.AiOrchestration.Functions;
using AISO.Domain.SalesOrders;
using AISO.Domain.Users;
using AISO.SapIntegration.Mock;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AISO.UnitTests;

public class MyProfileFunctionTests
{
    private static MyProfileFunction CreateFunction(out MockSapClient sap)
    {
        sap = new MockSapClient();
        var scope = new StubScopeLookup
        {
            Role = UserRole.Employee,
            SalesOrg = "TV01"
        };
        return new MyProfileFunction(sap, scope, NullLogger<MyProfileFunction>.Instance);
    }

    [Fact]
    public async Task MyProfile_ReturnsIdentityCountsAndTopOrders()
    {
        var fn = CreateFunction(out var sap);
        using var doc = JsonDocument.Parse("{}");

        var result = await fn.ExecuteAsync(doc.RootElement, "DEV-249");

        Assert.True(result.Success, result.ErrorMessage);
        var payload = Assert.IsType<MyProfileResponse>(result.Payload);
        Assert.Equal("DEV-249", payload.SapUser);
        Assert.Equal(UserRole.Employee, payload.Role);
        Assert.Equal("TV01", payload.SalesOrg);
        // MockSapClient returns a SAP user-role row for DEV-249 → expect SapUserRole.
        Assert.Equal(MyProfileSalesOrgSource.SapUserRole, payload.SalesOrgSource);
        Assert.True(payload.Counts.Total > 0);
        Assert.NotEmpty(payload.TopOrders);
        Assert.All(payload.TopOrders, o => Assert.Equal("DEV-249", o.OwnerSapUser));
        Assert.Null(payload.LoadError);
    }

    [Fact]
    public async Task MyProfile_OrdersAreOrderedByDateDesc()
    {
        var fn = CreateFunction(out _);
        using var doc = JsonDocument.Parse("{}");

        var result = await fn.ExecuteAsync(doc.RootElement, "DEV-249");

        var payload = (MyProfileResponse)result.Payload!;
        var dates = payload.TopOrders.Select(o => o.OrderDate).ToList();
        Assert.Equal(dates.OrderByDescending(d => d).ToList(), dates);
    }

    [Fact]
    public async Task MyProfile_ReturnsAtMostFiveTopOrders()
    {
        var fn = CreateFunction(out _);
        using var doc = JsonDocument.Parse("{}");

        var result = await fn.ExecuteAsync(doc.RootElement, "DEV-249");

        var payload = (MyProfileResponse)result.Payload!;
        Assert.True(payload.TopOrders.Count <= 5);
    }

    [Fact]
    public async Task MyProfile_FailsWhenNotLinked()
    {
        var fn = CreateFunction(out _);
        using var doc = JsonDocument.Parse("{}");

        var result = await fn.ExecuteAsync(doc.RootElement, "");

        Assert.False(result.Success);
        Assert.Equal("NOT_LINKED", result.ErrorCode);
    }

    [Fact]
    public async Task MyProfile_ReturnsEmptyCounts_WhenUserHasNoOrders()
    {
        var fn = CreateFunction(out _);
        using var doc = JsonDocument.Parse("{}");

        var result = await fn.ExecuteAsync(doc.RootElement, "DEV-NOBODY");

        Assert.True(result.Success);
        var payload = (MyProfileResponse)result.Payload!;
        Assert.Equal(0, payload.Counts.Total);
        Assert.Equal(MyProfileOrderCounts.Empty.Open, payload.Counts.Open);
        Assert.Empty(payload.TopOrders);
    }

    [Fact]
    public void OrderCountsFrom_AggregatesByStatus()
    {
        var orders = new[]
        {
            Order("1", SalesOrderStatus.Open),
            Order("2", SalesOrderStatus.Open),
            Order("3", SalesOrderStatus.Blocked),
            Order("4", SalesOrderStatus.Delivered),
            Order("5", SalesOrderStatus.Invoiced),
            Order("6", SalesOrderStatus.Cancelled),
            Order("7", SalesOrderStatus.PartiallyDelivered),
            Order("8", SalesOrderStatus.PartiallyDelivered),
        };

        var counts = MyProfileOrderCounts.From(orders);

        Assert.Equal(8, counts.Total);
        Assert.Equal(2, counts.Open);
        Assert.Equal(1, counts.Blocked);
        Assert.Equal(2, counts.PartiallyDelivered);
        Assert.Equal(1, counts.Delivered);
        Assert.Equal(1, counts.Invoiced);
        Assert.Equal(1, counts.Cancelled);
    }

    private static SalesOrder Order(string soNumber, SalesOrderStatus status) => new()
    {
        SoNumber = soNumber,
        CustomerId = "1000",
        CustomerName = "Test",
        OrderDate = new DateOnly(2026, 1, 1),
        NetValue = 100m,
        Currency = "USD",
        SalesOrg = "TV01",
        Status = status,
        OwnerSapUser = "DEV-249",
        Items = Array.Empty<SalesOrderItem>()
    };

    private sealed class StubScopeLookup : IUserScopeLookup
    {
        public UserRole Role { get; set; } = UserRole.Employee;
        public string? SalesOrg { get; set; }

        public Task<UserRole> GetRoleBySapUserAsync(string sapUserId, CancellationToken ct = default)
            => Task.FromResult(Role);

        public Task<string?> GetSalesOrgBySapUserAsync(string sapUserId, CancellationToken ct = default)
            => Task.FromResult(SalesOrg);

        public Task<string?> GetDelegatedBySapUserAsync(string sapUserId, CancellationToken ct = default)
            => Task.FromResult<string?>(null);

        public Task<string?> GetEmailBySapUserAsync(string sapUserId, CancellationToken ct = default)
            => Task.FromResult<string?>(null);

        public Task<DelegationInfo> GetDelegationInfoAsync(string sapUserId, CancellationToken ct = default)
            => Task.FromResult(new DelegationInfo(null, null));

        public Task SetDelegatedBySapUserAsync(string delegateUser, string? delegatorUser, DateTimeOffset? validTo = null, decimal? maxAmount = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ActiveDelegation>> GetActiveDelegationsAsync(string? filterDelegatorUser = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ActiveDelegation>>(Array.Empty<ActiveDelegation>());
    }
}
