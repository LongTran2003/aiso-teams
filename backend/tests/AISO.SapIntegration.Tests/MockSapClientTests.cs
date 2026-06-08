using AISO.Domain.SalesOrders;
using AISO.SapIntegration;
using AISO.SapIntegration.Mock;
using Xunit;

namespace AISO.SapIntegration.Tests;

public class MockSapClientTests
{
    private readonly ISapClient _sut = new MockSapClient();

    [Fact]
    public async Task GetSalesOrdersAsync_NoFilter_ReturnsOrdersOrderedByDateDescending()
    {
        var result = await _sut.GetSalesOrdersAsync(new SalesOrdersQuery());

        Assert.NotEmpty(result);
        for (var i = 1; i < result.Count; i++)
        {
            Assert.True(result[i - 1].OrderDate >= result[i].OrderDate);
        }
    }

    [Fact]
    public async Task GetSalesOrdersAsync_FilterByTop_LimitsResultCount()
    {
        var result = await _sut.GetSalesOrdersAsync(new SalesOrdersQuery { Top = 2 });

        Assert.True(result.Count <= 2);
    }

    [Theory]
    [InlineData("Philly", "Philly Bikes")]
    [InlineData("philly", "Philly Bikes")]
    [InlineData("PHILLY", "Philly Bikes")]
    [InlineData("Berlin", "Berlin Bikes")]
    public async Task GetSalesOrdersAsync_FilterByCustomerName_MatchesCaseInsensitively(
        string filter, string expectedCustomerName)
    {
        var result = await _sut.GetSalesOrdersAsync(
            new SalesOrdersQuery { CustomerIdOrName = filter });

        Assert.NotEmpty(result);
        Assert.All(result, order => Assert.Equal(expectedCustomerName, order.CustomerName));
    }

    [Fact]
    public async Task GetSalesOrdersAsync_FilterByCustomerId_MatchesExactId()
    {
        var result = await _sut.GetSalesOrdersAsync(
            new SalesOrdersQuery { CustomerIdOrName = "1000" });

        Assert.NotEmpty(result);
        Assert.All(result, order => Assert.Equal("1000", order.CustomerId));
    }

    [Fact]
    public async Task GetSalesOrdersAsync_FilterBySalesOrg_ReturnsOnlyMatchingOrg()
    {
        var result = await _sut.GetSalesOrdersAsync(
            new SalesOrdersQuery { SalesOrg = "UE00" });

        Assert.NotEmpty(result);
        Assert.All(result, order => Assert.Equal("UE00", order.SalesOrg));
    }

    [Fact]
    public async Task GetSalesOrdersAsync_FilterByStatus_ReturnsOnlyMatchingStatus()
    {
        var result = await _sut.GetSalesOrdersAsync(
            new SalesOrdersQuery { Status = SalesOrderStatus.Open });

        Assert.NotEmpty(result);
        Assert.All(result, order => Assert.Equal(SalesOrderStatus.Open, order.Status));
    }

    [Fact]
    public async Task GetSalesOrdersAsync_FilterByDateRange_RespectsBothBounds()
    {
        var from = new DateOnly(2026, 6, 1);
        var to = new DateOnly(2026, 6, 5);

        var result = await _sut.GetSalesOrdersAsync(
            new SalesOrdersQuery { FromDate = from, ToDate = to });

        Assert.NotEmpty(result);
        Assert.All(result, order =>
        {
            Assert.True(order.OrderDate >= from);
            Assert.True(order.OrderDate <= to);
        });
    }

    [Fact]
    public async Task GetSalesOrderByIdAsync_ExistingId_ReturnsOrder()
    {
        var result = await _sut.GetSalesOrderByIdAsync("0000005001");

        Assert.NotNull(result);
        Assert.Equal("Philly Bikes", result.CustomerName);
        Assert.NotEmpty(result.Items);
    }

    [Fact]
    public async Task GetSalesOrderByIdAsync_UnknownId_ReturnsNull()
    {
        var result = await _sut.GetSalesOrderByIdAsync("9999999999");

        Assert.Null(result);
    }
}
