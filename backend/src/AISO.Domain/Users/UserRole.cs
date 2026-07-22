namespace AISO.Domain.Users;

/// <summary>
/// Business role of a bot user, used for role-based access control (RBAC).
/// Ordered by privilege: Employee &lt; Manager &lt; Admin.
/// </summary>
public enum UserRole
{
    /// <summary>Order owner. Can query, create, reject/forward own orders and request release.</summary>
    Employee = 0,

    /// <summary>Approves releases and oversees a sales organization (VKORG).</summary>
    Manager = 1,

    /// <summary>Full system access, including overrides and audit.</summary>
    Admin = 2
}
