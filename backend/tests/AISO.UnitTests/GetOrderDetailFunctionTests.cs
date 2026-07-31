using AISO.AiOrchestration;
using AISO.AiOrchestration.Functions;
using AISO.SapIntegration.Mock;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AISO.UnitTests;

public class GetOrderDetailFunctionTests
{
    [Fact]
    public void FunctionRegistry_ResolvesGetOrderDetailAndCheckOrderStatus()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new MockSapClient());
        services.AddSingleton<AISO.SapIntegration.ISapClient>(sp => sp.GetRequiredService<MockSapClient>());
        services.AddSingleton<CheckOrderStatusFunction>();
        services.AddSingleton<IFunction>(sp => sp.GetRequiredService<CheckOrderStatusFunction>());
        services.AddSingleton<IFunction, GetOrderDetailFunction>();
        services.AddSingleton<IFunctionRegistry, FunctionRegistry>();
        services.AddLogging();

        using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IFunctionRegistry>();

        Assert.NotNull(registry.GetByName("CheckOrderStatus"));
        Assert.NotNull(registry.GetByName("GetOrderDetail"));
        Assert.Equal("GetOrderDetail", registry.GetByName("GetOrderDetail")!.Name);
    }

    [Fact]
    public async Task GetOrderDetail_ReturnsSameOrderAsCheckOrderStatus()
    {
        var sap = new MockSapClient();
        var check = new CheckOrderStatusFunction(sap, NullLogger<CheckOrderStatusFunction>.Instance);
        var detail = new GetOrderDetailFunction(check, NullLogger<GetOrderDetailFunction>.Instance);

        using var doc = System.Text.Json.JsonDocument.Parse("""{"order_id":"0000005001"}""");
        var fromCheck = await check.ExecuteAsync(doc.RootElement, "DEV-249");
        var fromDetail = await detail.ExecuteAsync(doc.RootElement, "DEV-249");

        Assert.True(fromCheck.Success);
        Assert.True(fromDetail.Success);
        Assert.Equal(fromCheck.Payload?.GetType(), fromDetail.Payload?.GetType());
    }
}
