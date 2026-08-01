using AISO.Bot.Cards.Builders;
using AISO.Bot.Services;
using AISO.Domain.Users;
using AISO.Persistence;
using AISO.Persistence.Entities;
using AISO.SapIntegration.Mock;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Xunit;

namespace AISO.UnitTests;

public class SsoDialogValidationTests
{
    [Fact]
    public void BuildLinkSapAccountCard_ShowsAssignedId_NotOtherUsers()
    {
        var attachment = TeamsCardBuilder.BuildLinkSapAccountCard(
            "Le Thi Thanh Thuy",
            assignedSapUserId: "DEV-024");
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.Contains("DEV-024", json);
        Assert.Contains("Your assigned ID", json);
        Assert.DoesNotContain("Also valid", json);
        Assert.DoesNotContain("DEV-249", json);
        Assert.Contains("Link SAP account", json);
    }

    [Fact]
    public void BuildLinkSapAccountCard_WithoutAssignment_ShowsFormatOnly()
    {
        var attachment = TeamsCardBuilder.BuildLinkSapAccountCard("Tran Long");
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.Contains("DEV-xxx", json);
        Assert.DoesNotContain("DEV-249", json);
        Assert.DoesNotContain("DEV-024", json);
    }

    [Fact]
    public void BuildWelcomeCard_MentionsSapUserIdNotDisplayName()
    {
        var attachment = TeamsCardBuilder.BuildWelcomeCard("Tran Long");
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.Contains("SAP User ID", json);
        Assert.Contains("DEV-xxx", json);
        Assert.DoesNotContain("DEV-249", json);
        Assert.DoesNotContain("LONGTNQ", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("DEV-249", true)]
    [InlineData("dev-024", true)]
    [InlineData("LONGTNQ", false)]
    [InlineData("not a user", false)]
    [InlineData("someone@fpt.edu.vn", false)]
    public async Task MockSapUserExists_AcceptsDemoIds(string sapUserId, bool expected)
    {
        var sap = new MockSapClient();
        var exists = await sap.SapUserExistsAsync(sapUserId);
        Assert.Equal(expected, exists);
    }
}

public class SapLinkAssignmentTests
{
    private static AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task FindLinkAssignment_MatchesByEmail_CaseInsensitive()
    {
        using var ctx = NewContext();
        ctx.SapLinkAssignments.Add(new SapLinkAssignment
        {
            SapUserId = "DEV-024",
            TeamsEmail = "lethuy@aisoteam.onmicrosoft.com",
            Role = UserRole.Employee,
            SalesOrg = "TV01"
        });
        await ctx.SaveChangesAsync();
        var service = new UserMappingService(ctx);

        var found = await service.FindLinkAssignmentAsync(
            "teams-thuy",
            "LeThuy@AISOTeam.onmicrosoft.com");

        Assert.NotNull(found);
        Assert.Equal("DEV-024", found!.SapUserId);
    }

    [Fact]
    public async Task FindLinkAssignment_MatchesByTeamsUserId()
    {
        using var ctx = NewContext();
        ctx.SapLinkAssignments.Add(new SapLinkAssignment
        {
            SapUserId = "DEV-249",
            TeamsUserId = "teams-long",
            Role = UserRole.Manager,
            SalesOrg = "TV01"
        });
        await ctx.SaveChangesAsync();
        var service = new UserMappingService(ctx);

        var found = await service.FindLinkAssignmentAsync("teams-long", teamsEmail: null);

        Assert.NotNull(found);
        Assert.Equal("DEV-249", found!.SapUserId);
    }

    [Fact]
    public async Task IsSapUserLinkedToOtherTeamsUser_DetectsConflict()
    {
        using var ctx = NewContext();
        var service = new UserMappingService(ctx);
        await service.MapUserAsync("teams-tien", "Tien", "DEV-024");

        Assert.True(await service.IsSapUserLinkedToOtherTeamsUserAsync("DEV-024", "teams-thuy"));
        Assert.False(await service.IsSapUserLinkedToOtherTeamsUserAsync("DEV-024", "teams-tien"));
    }

    [Fact]
    public async Task MapUser_AppliesRoleAndSalesOrgFromAssignment()
    {
        using var ctx = NewContext();
        var service = new UserMappingService(ctx);

        await service.MapUserAsync(
            "teams-thuy",
            "Thuy",
            "DEV-024",
            role: UserRole.Employee,
            salesOrg: "TV01");

        Assert.Equal(UserRole.Employee, await service.GetRoleAsync("teams-thuy"));
        Assert.Equal("TV01", await service.GetSalesOrgAsync("teams-thuy"));
    }
}
