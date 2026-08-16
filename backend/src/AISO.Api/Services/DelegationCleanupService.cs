using AISO.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AISO.Api.Services;

/// <summary>
/// Periodically scans for expired delegations and revokes them automatically.
/// </summary>
public class DelegationCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DelegationCleanupService> _logger;

    public DelegationCleanupService(IServiceProvider serviceProvider, ILogger<DelegationCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DelegationCleanupService started.");

        // Run every 1 hour
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredDelegationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing DelegationCleanupService.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task CleanupExpiredDelegationsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var now = DateTimeOffset.UtcNow;

        var expiredMappings = await db.UserMappings
            .Where(u => u.DelegatedBySapUser != null && u.DelegatedValidTo != null && u.DelegatedValidTo < now)
            .ToListAsync(ct);

        foreach (var mapping in expiredMappings)
        {
            _logger.LogInformation("Revoking expired delegation for UserMapping {UserId}", mapping.SapUserId);
            mapping.DelegatedBySapUser = null;
            mapping.DelegatedValidTo = null;
            mapping.UpdatedAt = now;
        }

        var expiredAssignments = await db.SapLinkAssignments
            .Where(a => a.DelegatedBySapUser != null && a.DelegatedValidTo != null && a.DelegatedValidTo < now)
            .ToListAsync(ct);

        foreach (var assignment in expiredAssignments)
        {
            _logger.LogInformation("Revoking expired delegation for SapLinkAssignment {UserId}", assignment.SapUserId);
            assignment.DelegatedBySapUser = null;
            assignment.DelegatedValidTo = null;
            assignment.UpdatedAt = now;
        }

        if (expiredMappings.Count > 0 || expiredAssignments.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully cleaned up {Count} expired delegations.", expiredMappings.Count + expiredAssignments.Count);
        }
    }
}
