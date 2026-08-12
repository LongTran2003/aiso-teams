using AISO.Domain.Users;

namespace AISO.Persistence.Entities;

/// <summary>
/// Admin-provisioned allow-list: which Teams identity may link which SAP User ID.
/// Prevents users from typing another person's SAP ID at bot login.
/// </summary>
public class SapLinkAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>SAP user ID that this person is allowed to use (e.g. DEV-024).</summary>
    public string SapUserId { get; set; } = string.Empty;

    /// <summary>Teams / Entra email (normalized lowercase). Preferred match key.</summary>
    public string? TeamsEmail { get; set; }

    /// <summary>Teams user id once known; optional until first successful link.</summary>
    public string? TeamsUserId { get; set; }

    public UserRole Role { get; set; } = UserRole.Employee;

    public string? SalesOrg { get; set; }

    /// <summary>SAP ID of the Manager who delegated their approval rights to this user.</summary>
    public string? DelegatedBySapUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
