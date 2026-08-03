using AISO.Domain.Users;
using AISO.Persistence;
using AISO.Persistence.Entities;
using AISO.Persistence.Users;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AISO.UnitTests;

public class BotUserAdminServiceTests
{
    private static BotUserAdminService CreateService(out AppDbContext ctx)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        ctx = new AppDbContext(options);
        return new BotUserAdminService(new TestDbContextFactory(options));
    }

    [Fact]
    public async Task ListLinkedUsers_ReturnsMappedUsers()
    {
        var service = CreateService(out var ctx);
        await using (ctx)
        {
            ctx.UserMappings.Add(new UserMapping
            {
                TeamsUserId = "t1",
                DisplayName = "Alice",
                SapUserId = "DEV-001",
                Role = UserRole.Employee,
                SalesOrg = "TV01"
            });
            ctx.SapLinkAssignments.Add(new SapLinkAssignment
            {
                SapUserId = "DEV-001",
                TeamsEmail = "alice@example.com",
                Role = UserRole.Employee,
                SalesOrg = "TV01"
            });
            await ctx.SaveChangesAsync();

            var users = await service.ListLinkedUsersAsync();

            Assert.Single(users);
            Assert.Equal("DEV-001", users[0].SapUserId);
            Assert.True(users[0].HasLinkAssignment);
        }
    }

    [Fact]
    public async Task UpdateAccess_UpdatesMappingAndAssignment()
    {
        var service = CreateService(out var ctx);
        await using (ctx)
        {
            ctx.UserMappings.Add(new UserMapping
            {
                TeamsUserId = "t1",
                DisplayName = "Bob",
                SapUserId = "DEV-002",
                Role = UserRole.Employee,
                SalesOrg = "TV01"
            });
            ctx.SapLinkAssignments.Add(new SapLinkAssignment
            {
                SapUserId = "DEV-002",
                TeamsEmail = "bob@example.com",
                Role = UserRole.Employee,
                SalesOrg = "TV01"
            });
            await ctx.SaveChangesAsync();

            var updated = await service.UpdateAccessAsync("dev-002", UserRole.Manager, "FU24");

            Assert.Equal(UserRole.Manager, updated.Role);
            Assert.Equal("FU24", updated.SalesOrg);

            ctx.ChangeTracker.Clear();
            var mapping = await ctx.UserMappings.SingleAsync();
            var assignment = await ctx.SapLinkAssignments.SingleAsync();
            Assert.Equal(UserRole.Manager, mapping.Role);
            Assert.Equal("FU24", mapping.SalesOrg);
            Assert.Equal(UserRole.Manager, assignment.Role);
            Assert.Equal("FU24", assignment.SalesOrg);
        }
    }

    [Fact]
    public async Task UpdateAccess_Admin_ClearsSalesOrg()
    {
        var service = CreateService(out var ctx);
        await using (ctx)
        {
            ctx.UserMappings.Add(new UserMapping
            {
                TeamsUserId = "t1",
                DisplayName = "Admin",
                SapUserId = "DEV-100",
                Role = UserRole.Manager,
                SalesOrg = "TV01"
            });
            await ctx.SaveChangesAsync();

            var updated = await service.UpdateAccessAsync("DEV-100", UserRole.Admin, "TV01");

            Assert.Equal(UserRole.Admin, updated.Role);
            Assert.Null(updated.SalesOrg);
        }
    }

    [Fact]
    public async Task UpdateAccess_UnknownSalesOrg_Throws()
    {
        var service = CreateService(out var ctx);
        await using (ctx)
        {
            ctx.UserMappings.Add(new UserMapping
            {
                TeamsUserId = "t1",
                DisplayName = "Eve",
                SapUserId = "DEV-003",
                Role = UserRole.Employee,
                SalesOrg = "TV01"
            });
            await ctx.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdateAccessAsync("DEV-003", UserRole.Manager, "ZZ99"));
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
