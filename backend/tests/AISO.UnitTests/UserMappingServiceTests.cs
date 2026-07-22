using AISO.Bot.Services;
using AISO.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AISO.UnitTests;

public class UserMappingServiceTests
{
    private static AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task MapUser_ThenGetSapUsername_ReturnsMappedId()
    {
        using var ctx = NewContext();
        var service = new UserMappingService(ctx);

        await service.MapUserAsync("teams-1", "Long", "DEV-249");

        var sapId = await service.GetSapUsernameAsync("teams-1");
        Assert.Equal("DEV-249", sapId);
    }

    [Fact]
    public async Task MapUser_Twice_UpdatesExistingMapping()
    {
        using var ctx = NewContext();
        var service = new UserMappingService(ctx);

        await service.MapUserAsync("teams-1", "Long", "DEV-249");
        await service.MapUserAsync("teams-1", "Long Tran", "DEV-300");

        Assert.Equal("DEV-300", await service.GetSapUsernameAsync("teams-1"));
        Assert.Single(ctx.UserMappings);
    }

    [Fact]
    public async Task GetSapUsername_WhenUnknownUser_ReturnsNull()
    {
        using var ctx = NewContext();
        var service = new UserMappingService(ctx);

        Assert.Null(await service.GetSapUsernameAsync("nobody"));
    }

    [Fact]
    public async Task GetForwardRecipientChoices_FormatsTitleAndUsesSapIdAsValue()
    {
        using var ctx = NewContext();
        var service = new UserMappingService(ctx);
        await service.MapUserAsync("teams-1", "Long", "DEV-249");

        var choices = await service.GetForwardRecipientChoicesAsync();

        var choice = Assert.Single(choices);
        Assert.Equal("Long (DEV-249)", choice.Title);
        Assert.Equal("DEV-249", choice.Value);
    }

    [Fact]
    public async Task GetDisplayName_WhenDisplayNameBlank_FallsBackToSapId()
    {
        using var ctx = NewContext();
        var service = new UserMappingService(ctx);
        await service.MapUserAsync("teams-1", "", "DEV-249");

        Assert.Equal("DEV-249", await service.GetDisplayNameAsync("teams-1"));
    }

    [Fact]
    public async Task RemoveMapping_DeletesTheEntry()
    {
        using var ctx = NewContext();
        var service = new UserMappingService(ctx);
        await service.MapUserAsync("teams-1", "Long", "DEV-249");

        await service.RemoveMappingAsync("teams-1");

        Assert.Empty(ctx.UserMappings);
        Assert.Null(await service.GetSapUsernameAsync("teams-1"));
    }
}
