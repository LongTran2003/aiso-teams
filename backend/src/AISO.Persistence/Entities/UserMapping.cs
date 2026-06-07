namespace AISO.Persistence.Entities;

/// <summary>
/// Maps a Microsoft Teams (Entra ID) user to a SAP user.
/// Populated when a user first authenticates with the bot.
/// </summary>
public class UserMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Entra ID object ID (immutable across sessions).</summary>
    public string TeamsUserId { get; set; } = string.Empty;

    /// <summary>User display name as shown in Teams.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>SAP user ID (e.g. "PHILLY01"). Nullable until first mapping.</summary>
    public string? SapUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
