using AISO.Domain.Users;

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

    /// <summary>Business role for RBAC. Defaults to <see cref="UserRole.Employee"/>.</summary>
    public UserRole Role { get; set; } = UserRole.Employee;

    /// <summary>Sales organization (VKORG) this user is scoped to. Used for Manager scoping. Nullable.</summary>
    public string? SalesOrg { get; set; }

    /// <summary>SAP ID of the Manager who delegated their approval rights to this user.</summary>
    public string? DelegatedBySapUser { get; set; }

    /// <summary>Expiration date of the delegation.</summary>
    public DateTimeOffset? DelegatedValidTo { get; set; }

    /// <summary>The maximum order amount this user is allowed to approve (partial delegation).</summary>
    public decimal? DelegationMaxAmount { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
