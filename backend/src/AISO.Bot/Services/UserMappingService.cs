using AISO.Domain.Entities;
using AISO.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AISO.Bot.Services;

public class UserMappingService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<UserMappingService> _logger;

    public UserMappingService(AppDbContext dbContext, ILogger<UserMappingService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Gets the SapUsername mapped to the given Teams user ID, if it exists.
    /// </summary>
    public async Task<string?> GetSapUsernameAsync(string teamsUserId, CancellationToken cancellationToken = default)
    {
        var mapping = await _dbContext.UserMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TeamsUserId == teamsUserId, cancellationToken);
            
        return mapping?.SapUsername;
    }

    /// <summary>
    /// Maps a Teams user to an SAP username. If a mapping already exists, it is updated.
    /// </summary>
    public async Task MapUserAsync(string teamsUserId, string teamsEmail, string sapUsername, CancellationToken cancellationToken = default)
    {
        var mapping = await _dbContext.UserMappings
            .FirstOrDefaultAsync(m => m.TeamsUserId == teamsUserId, cancellationToken);

        if (mapping == null)
        {
            mapping = new UserMapping
            {
                Id = Guid.NewGuid(),
                TeamsUserId = teamsUserId,
                TeamsEmail = teamsEmail,
                SapUsername = sapUsername,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.UserMappings.Add(mapping);
            _logger.LogInformation("Created new user mapping for Teams ID {TeamsUserId} to SAP ID {SapUsername}", teamsUserId, sapUsername);
        }
        else
        {
            mapping.SapUsername = sapUsername;
            mapping.TeamsEmail = teamsEmail;
            mapping.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation("Updated user mapping for Teams ID {TeamsUserId} to new SAP ID {SapUsername}", teamsUserId, sapUsername);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
