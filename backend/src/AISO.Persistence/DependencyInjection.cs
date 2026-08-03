using AISO.Domain.Approvals;
using AISO.Domain.Auditing;
using AISO.Domain.Users;
using AISO.Persistence.Approvals;
using AISO.Persistence.Auditing;
using AISO.Persistence.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AISO.Persistence;

public static class DependencyInjection
{
    /// <summary>
    /// Registers AppDbContext using the "Postgres" connection string.
    /// </summary>
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Connection string 'Postgres' not found in configuration. " +
                "Check appsettings.Development.json.");

        // Factory is Singleton (safe for singleton AI functions). Scoped AppDbContext
        // is created per request from the factory so UserMappingService/audit still work.
        services.AddDbContextFactory<AppDbContext>(opts =>
            opts.UseNpgsql(connectionString));
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

        // Audit logger — Scoped to share the per-turn DbContext.
        services.AddScoped<IAuditLogger, EfAuditLogger>();

        // Maker-checker approval store (singleton-safe via DbContextFactory).
        services.AddSingleton<IOrderApprovalService, OrderApprovalService>();
        services.AddSingleton<IUserScopeLookup, UserScopeLookup>();
        services.AddSingleton<IBotUserAdminService, BotUserAdminService>();
        services.AddSingleton<IAuditQuery, EfAuditQuery>();

        return services;
    }
}
