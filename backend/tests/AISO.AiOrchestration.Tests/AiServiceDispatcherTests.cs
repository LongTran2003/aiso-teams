using System.Text.Json;
using AISO.AiOrchestration.Functions;
using AISO.AiOrchestration.Stub;
using AISO.SapIntegration;
using AISO.SapIntegration.Mock;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AISO.AiOrchestration.Tests;

/// <summary>
/// Tests for KeywordFunctionDispatcher — the fallback dispatcher used when
/// AiService:UseKeywordFallback=true (default in dev).
/// </summary>
public class KeywordFunctionDispatcherTests
{
    private readonly IFunctionRegistry _registry;
    private readonly KeywordFunctionDispatcher _sut;

    public KeywordFunctionDispatcherTests()
    {
        ISapClient sap = new MockSapClient();
        var functions = new IFunction[]
        {
            new GetSalesOrdersFunction(sap, Substitute.For<ILogger<GetSalesOrdersFunction>>()),
            new CheckOrderStatusFunction(sap, Substitute.For<ILogger<CheckOrderStatusFunction>>()),
            new ReleaseOrderFunction(Substitute.For<ILogger<ReleaseOrderFunction>>()),
            new RejectOrderFunction(Substitute.For<ILogger<RejectOrderFunction>>()),
            new ForwardOrderFunction(Substitute.For<ILogger<ForwardOrderFunction>>()),
        };
        _registry = new FunctionRegistry(functions);
        _sut = new KeywordFunctionDispatcher(_registry);
    }

    [Theory]
    [InlineData("show orders")]
    [InlineData("đơn hàng gần đây")]
    [InlineData("list all orders")]
    public async Task DispatchAsync_OrderKeywords_CallsGetSalesOrders(string message)
    {
        var result = await _sut.DispatchAsync(message);

        Assert.True(result.Handled);
        Assert.Equal("GetSalesOrders", result.FunctionName);
        Assert.True(result.Result?.Success);
    }

    [Theory]
    [InlineData("kiểm tra đơn hàng 5001")]
    [InlineData("check order 5003")]
    public async Task DispatchAsync_CheckOrderPattern_CallsCheckOrderStatus(string message)
    {
        var result = await _sut.DispatchAsync(message);

        Assert.True(result.Handled);
        Assert.Equal("CheckOrderStatus", result.FunctionName);
        Assert.True(result.Result?.Success);
    }

    [Fact]
    public async Task DispatchAsync_CheckNonExistentOrder_ReturnsFail()
    {
        var result = await _sut.DispatchAsync("kiểm tra đơn hàng 9999999");

        Assert.True(result.Handled);
        Assert.Equal("CheckOrderStatus", result.FunctionName);
        Assert.False(result.Result?.Success);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("what's up")]
    [InlineData("")]
    public async Task DispatchAsync_UnknownIntent_ReturnsUnhandled(string message)
    {
        var result = await _sut.DispatchAsync(message);

        Assert.False(result.Handled);
        Assert.Equal("intent unclear", result.Reason);
    }
}

/// <summary>
/// Tests for FunctionRegistry — verifies all 5 functions are registered
/// and can be looked up by name (case-insensitive).
/// </summary>
public class FunctionRegistryTests
{
    private readonly FunctionRegistry _registry;

    public FunctionRegistryTests()
    {
        var functions = new IFunction[]
        {
            new GetSalesOrdersFunction(new MockSapClient(),
                Substitute.For<ILogger<GetSalesOrdersFunction>>()),
            new CheckOrderStatusFunction(new MockSapClient(),
                Substitute.For<ILogger<CheckOrderStatusFunction>>()),
            new ReleaseOrderFunction(Substitute.For<ILogger<ReleaseOrderFunction>>()),
            new RejectOrderFunction(Substitute.For<ILogger<RejectOrderFunction>>()),
            new ForwardOrderFunction(Substitute.For<ILogger<ForwardOrderFunction>>()),
        };
        _registry = new FunctionRegistry(functions);
    }

    [Fact]
    public void All_Returns5Functions()
    {
        Assert.Equal(5, _registry.All.Count);
    }

    [Theory]
    [InlineData("GetSalesOrders")]
    [InlineData("CheckOrderStatus")]
    [InlineData("ReleaseOrder")]
    [InlineData("RejectOrder")]
    [InlineData("ForwardOrder")]
    public void GetByName_ExistingFunction_ReturnsFunction(string name)
    {
        var fn = _registry.GetByName(name);
        Assert.NotNull(fn);
        Assert.Equal(name, fn.Name);
    }

    [Theory]
    [InlineData("getsalesorders")]
    [InlineData("CHECKORDERSTATUS")]
    [InlineData("releaseorder")]
    public void GetByName_CaseInsensitive_ReturnsFunction(string name)
    {
        var fn = _registry.GetByName(name);
        Assert.NotNull(fn);
    }

    [Fact]
    public void GetByName_NonExistent_ReturnsNull()
    {
        var fn = _registry.GetByName("DoesNotExist");
        Assert.Null(fn);
    }
}
