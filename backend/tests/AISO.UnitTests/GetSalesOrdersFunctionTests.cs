using System.Text.Json;
using AISO.AiOrchestration.Functions;
using AISO.SapIntegration.Mock;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AISO.UnitTests;

public class GetSalesOrdersFunctionTests
{
    [Fact]
    public async Task OwnedByMe_FiltersToRequestingUserAndSetsTitle()
    {
        var sap = new MockSapClient();
        var fn = new GetSalesOrdersFunction(sap, NullLogger<GetSalesOrdersFunction>.Instance);

        using var doc = JsonDocument.Parse("""{"ownedByMe":true,"top":20}""");
        var result = await fn.ExecuteAsync(doc.RootElement, "DEV-249");

        Assert.True(result.Success);
        var payload = Assert.IsType<GetSalesOrdersResponse>(result.Payload);
        Assert.Equal("My sales orders", payload.Title);
        Assert.NotEmpty(payload.Orders);
        Assert.All(payload.Orders, o => Assert.Equal("DEV-249", o.OwnerSapUser));
    }

    [Fact]
    public async Task WithoutOwnedByMe_DoesNotFilterByOwner()
    {
        var sap = new MockSapClient();
        var fn = new GetSalesOrdersFunction(sap, NullLogger<GetSalesOrdersFunction>.Instance);

        using var doc = JsonDocument.Parse("""{"top":20}""");
        var result = await fn.ExecuteAsync(doc.RootElement, "DEV-249");

        Assert.True(result.Success);
        var payload = Assert.IsType<GetSalesOrdersResponse>(result.Payload);
        Assert.Equal("Sales orders", payload.Title);
        Assert.Contains(payload.Orders, o => o.OwnerSapUser == "DEV-024");
        Assert.Contains(payload.Orders, o => o.OwnerSapUser == "DEV-249");
    }

    [Fact]
    public async Task OwnedByMe_WithNoMatchingOwner_ReturnsEmpty()
    {
        var sap = new MockSapClient();
        var fn = new GetSalesOrdersFunction(sap, NullLogger<GetSalesOrdersFunction>.Instance);

        using var doc = JsonDocument.Parse("""{"ownedByMe":true}""");
        var result = await fn.ExecuteAsync(doc.RootElement, "DEV-999");

        Assert.True(result.Success);
        var payload = Assert.IsType<GetSalesOrdersResponse>(result.Payload);
        Assert.Equal("My sales orders", payload.Title);
        Assert.Empty(payload.Orders);
    }
}
