using AISO.Bot.Services;
using AISO.Domain.Users;
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
    public async Task GetRole_WhenUnknownUser_DefaultsToEmployee()
    {
        using var ctx = NewContext();
        var service = new UserMappingService(ctx);

        Assert.Equal(UserRole.Employee, await service.GetRoleAsync("nobody"));
    }

    [Fact]
    public async Task GetRole_ReturnsAssignedRole()
    {
        using var ctx = NewContext();
        var service = new UserMappingService(ctx);
        await service.MapUserAsync("teams-1", "Long", "DEV-249");

        // New mappings default to Employee.
        Assert.Equal(UserRole.Employee, await service.GetRoleAsync("teams-1"));

        // Simulate an admin assigning the Manager role.
        var mapping = ctx.UserMappings.Single(u => u.TeamsUserId == "teams-1");
        mapping.Role = UserRole.Manager;
        await ctx.SaveChangesAsync();

        Assert.Equal(UserRole.Manager, await service.GetRoleAsync("teams-1"));
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
    public async Task GetForwardRecipientChoices_ExcludesCurrentUser()
    {
        using var ctx = NewContext();
        var service = new UserMappingService(ctx);
        await service.MapUserAsync("teams-1", "Long", "DEV-249");
        await service.MapUserAsync("teams-2", "Thuy", "DEV-244");

        var choices = await service.GetForwardRecipientChoicesAsync(excludeSapUserId: "DEV-249");

        var choice = Assert.Single(choices);
        Assert.Equal("DEV-244", choice.Value);
    }

    [Fact]
    public async Task GetForwardRecipientChoices_FiltersByOrderSalesOrg()
    {
        using var ctx = NewContext();
        var service = new UserMappingService(ctx);
        await service.MapUserAsync("teams-1", "Long", "DEV-249", role: UserRole.Employee, salesOrg: "TV01");
        await service.MapUserAsync("teams-2", "Thuy", "DEV-244", role: UserRole.Employee, salesOrg: "TV01");
        await service.MapUserAsync("teams-3", "Tien", "DEV-024", role: UserRole.Manager, salesOrg: "FU24");

        var choices = await service.GetForwardRecipientChoicesAsync(
            excludeSapUserId: "DEV-249",
            salesOrgFromOrder: "TV01");

        var choice = Assert.Single(choices);
        Assert.Equal("DEV-244", choice.Value);
    }

    [Fact]
    public async Task GetForwardRecipientChoices_IncludesUnscopedUsersForOrderOrg()
    {
        using var ctx = NewContext();
        var service = new UserMappingService(ctx);
        await service.MapUserAsync("teams-1", "Long", "DEV-249", role: UserRole.Employee, salesOrg: "TV01");
        await service.MapUserAsync("teams-2", "Thuy", "DEV-244", role: UserRole.Employee, salesOrg: null);
        await service.MapUserAsync("teams-3", "Tien", "DEV-024", role: UserRole.Manager, salesOrg: "FU24");

        var choices = await service.GetForwardRecipientChoicesAsync(
            excludeSapUserId: "DEV-249",
            salesOrgFromOrder: "TV01");

        Assert.Equal(new[] { "DEV-244" }, choices.Select(c => c.Value).ToArray());
    }

    [Fact]
    public async Task GetForwardRecipientChoices_WhenNoOrgMatch_FallsBackToAllExceptSelf()
    {
        using var ctx = NewContext();
        var service = new UserMappingService(ctx);
        await service.MapUserAsync("teams-1", "Long", "DEV-249", role: UserRole.Employee, salesOrg: "TV01");
        await service.MapUserAsync("teams-3", "Tien", "DEV-024", role: UserRole.Manager, salesOrg: "FU24");

        // Only self matches TV01 → after exclude, scoped empty → fallback keeps FU24 manager.
        var choices = await service.GetForwardRecipientChoicesAsync(
            excludeSapUserId: "DEV-249",
            salesOrgFromOrder: "TV01");

        var choice = Assert.Single(choices);
        Assert.Equal("DEV-024", choice.Value);
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
