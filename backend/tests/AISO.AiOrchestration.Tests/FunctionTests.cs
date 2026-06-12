using System.Text.Json;
using AISO.AiOrchestration.Functions;
using AISO.SapIntegration;
using AISO.SapIntegration.Mock;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AISO.AiOrchestration.Tests;

public class CheckOrderStatusFunctionTests
{
    private readonly ISapClient _sap = new MockSapClient();
    private readonly ILogger<CheckOrderStatusFunction> _logger =
        Substitute.For<ILogger<CheckOrderStatusFunction>>();

    [Fact]
    public async Task ExecuteAsync_ValidOrderId_ReturnsOrder()
    {
        var fn = new CheckOrderStatusFunction(_sap, _logger);
        var parameters = JsonDocument.Parse("""{"order_id": "0000005001"}""").RootElement;

        var result = await fn.ExecuteAsync(parameters);

        Assert.True(result.Success);
        Assert.NotNull(result.Payload);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownOrderId_ReturnsFail()
    {
        var fn = new CheckOrderStatusFunction(_sap, _logger);
        var parameters = JsonDocument.Parse("""{"order_id": "9999999999"}""").RootElement;

        var result = await fn.ExecuteAsync(parameters);

        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_MissingOrderId_ReturnsFail()
    {
        var fn = new CheckOrderStatusFunction(_sap, _logger);
        var parameters = JsonDocument.Parse("{}").RootElement;

        var result = await fn.ExecuteAsync(parameters);

        Assert.False(result.Success);
        Assert.Contains("order_id", result.ErrorMessage);
    }
}

public class ReleaseOrderFunctionTests
{
    private readonly ILogger<ReleaseOrderFunction> _logger =
        Substitute.For<ILogger<ReleaseOrderFunction>>();

    [Fact]
    public async Task ExecuteAsync_ValidParams_ReturnsSuccess()
    {
        var fn = new ReleaseOrderFunction(_logger);
        var parameters = JsonDocument.Parse(
            """{"order_id": "0000005001", "comment": "Approved by manager"}""").RootElement;

        var result = await fn.ExecuteAsync(parameters);

        Assert.True(result.Success);
        var json = JsonSerializer.Serialize(result.Payload);
        Assert.Contains("released successfully", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutComment_StillSucceeds()
    {
        var fn = new ReleaseOrderFunction(_logger);
        var parameters = JsonDocument.Parse("""{"order_id": "0000005001"}""").RootElement;

        var result = await fn.ExecuteAsync(parameters);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ExecuteAsync_MissingOrderId_ReturnsFail()
    {
        var fn = new ReleaseOrderFunction(_logger);
        var parameters = JsonDocument.Parse("{}").RootElement;

        var result = await fn.ExecuteAsync(parameters);

        Assert.False(result.Success);
        Assert.Contains("order_id", result.ErrorMessage);
    }
}

public class RejectOrderFunctionTests
{
    private readonly ILogger<RejectOrderFunction> _logger =
        Substitute.For<ILogger<RejectOrderFunction>>();

    [Fact]
    public async Task ExecuteAsync_ValidParams_ReturnsSuccess()
    {
        var fn = new RejectOrderFunction(_logger);
        var parameters = JsonDocument.Parse(
            """{"order_id": "0000005003", "reason_code": "PRICE_ISSUE"}""").RootElement;

        var result = await fn.ExecuteAsync(parameters);

        Assert.True(result.Success);
        var json = JsonSerializer.Serialize(result.Payload);
        Assert.Contains("rejected", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PRICE_ISSUE", json);
    }

    [Fact]
    public async Task ExecuteAsync_MissingReasonCode_ReturnsFail()
    {
        var fn = new RejectOrderFunction(_logger);
        var parameters = JsonDocument.Parse("""{"order_id": "0000005003"}""").RootElement;

        var result = await fn.ExecuteAsync(parameters);

        Assert.False(result.Success);
        Assert.Contains("reason_code", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_MissingOrderId_ReturnsFail()
    {
        var fn = new RejectOrderFunction(_logger);
        var parameters = JsonDocument.Parse("""{"reason_code": "OTHER"}""").RootElement;

        var result = await fn.ExecuteAsync(parameters);

        Assert.False(result.Success);
        Assert.Contains("order_id", result.ErrorMessage);
    }
}

public class ForwardOrderFunctionTests
{
    private readonly ILogger<ForwardOrderFunction> _logger =
        Substitute.For<ILogger<ForwardOrderFunction>>();

    [Fact]
    public async Task ExecuteAsync_ValidParams_ReturnsSuccess()
    {
        var fn = new ForwardOrderFunction(_logger);
        var parameters = JsonDocument.Parse(
            """{"order_id": "0000005001", "forward_to_user": "jane.doe@example.com"}""").RootElement;

        var result = await fn.ExecuteAsync(parameters);

        Assert.True(result.Success);
        var json = JsonSerializer.Serialize(result.Payload);
        Assert.Contains("forwarded", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("jane.doe@example.com", json);
    }

    [Fact]
    public async Task ExecuteAsync_MissingForwardTo_ReturnsFail()
    {
        var fn = new ForwardOrderFunction(_logger);
        var parameters = JsonDocument.Parse("""{"order_id": "0000005001"}""").RootElement;

        var result = await fn.ExecuteAsync(parameters);

        Assert.False(result.Success);
        Assert.Contains("forward_to_user", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_MissingOrderId_ReturnsFail()
    {
        var fn = new ForwardOrderFunction(_logger);
        var parameters = JsonDocument.Parse(
            """{"forward_to_user": "jane@example.com"}""").RootElement;

        var result = await fn.ExecuteAsync(parameters);

        Assert.False(result.Success);
        Assert.Contains("order_id", result.ErrorMessage);
    }
}
