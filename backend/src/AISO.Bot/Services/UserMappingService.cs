using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AISO.Persistence;
using AISO.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AISO.Bot.Services;

public class UserMappingService
{
    private readonly AppDbContext _dbContext;

    public UserMappingService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string?> GetSapUsernameAsync(string teamsUserId, CancellationToken cancellationToken = default)
    {
        var mapping = await _dbContext.UserMappings
            .Where(u => u.TeamsUserId == teamsUserId)
            .FirstOrDefaultAsync(cancellationToken);

        return mapping?.SapUserId;
    }

    public async Task<IReadOnlyList<(string Title, string Value)>> GetForwardRecipientChoicesAsync(CancellationToken cancellationToken = default)
    {
        var mappings = await _dbContext.UserMappings
            .Where(u => !string.IsNullOrWhiteSpace(u.TeamsUserId) && !string.IsNullOrWhiteSpace(u.SapUserId))
            .OrderBy(u => u.DisplayName)
            .ThenBy(u => u.SapUserId)
            .Select(u => new { u.DisplayName, u.SapUserId, u.TeamsUserId })
            .ToListAsync(cancellationToken);

        // Value is the Teams User ID so the SAP owner (zaiso_so_map.teams_user_id)
        // is stored in the same identity used by every owner check.
        return mappings
            .Select(mapping => (
                Title: string.IsNullOrWhiteSpace(mapping.DisplayName)
                    ? mapping.SapUserId!
                    : $"{mapping.DisplayName} ({mapping.SapUserId})",
                Value: mapping.TeamsUserId))
            .ToList();
    }

    public async Task<string?> GetDisplayNameAsync(string teamsUserId, CancellationToken cancellationToken = default)
    {
        var mapping = await _dbContext.UserMappings
            .Where(u => u.TeamsUserId == teamsUserId)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(mapping?.DisplayName) ? mapping?.SapUserId : mapping.DisplayName;
    }

    public async Task MapUserAsync(string teamsUserId, string displayName, string sapUserId, CancellationToken cancellationToken = default)
    {
        var mapping = await _dbContext.UserMappings
            .Where(u => u.TeamsUserId == teamsUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (mapping == null)
        {
            mapping = new UserMapping
            {
                TeamsUserId = teamsUserId,
                DisplayName = displayName,
                SapUserId = sapUserId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _dbContext.UserMappings.Add(mapping);
        }
        else
        {
            mapping.DisplayName = displayName;
            mapping.SapUserId = sapUserId;
            mapping.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveMappingAsync(string teamsUserId, CancellationToken cancellationToken = default)
    {
        var mapping = await _dbContext.UserMappings
            .Where(u => u.TeamsUserId == teamsUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (mapping != null)
        {
            _dbContext.UserMappings.Remove(mapping);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
