using AISO.Persistence.Auditing;
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

        services.AddDbContext<AppDbContext>(opts =>
            opts.UseNpgsql(connectionString));

        // Audit logger — Scoped to share the per-turn DbContext.
        services.AddScoped<IAuditLogger, EfAuditLogger>();

        return services;
    }
}
