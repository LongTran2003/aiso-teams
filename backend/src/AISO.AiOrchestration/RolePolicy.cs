using AISO.Domain.Users;

namespace AISO.AiOrchestration;

/// <summary>
/// Central role-based access policy (Phase B: enforced in the Backend).
/// Maps a function name to the minimum <see cref="UserRole"/> required to run it.
/// Functions not listed require <see cref="UserRole.Employee"/> (i.e. available to everyone
/// signed in), so read/query functions and chitchat are open by default while sensitive
/// write actions are explicitly elevated.
/// </summary>
public static class RolePolicy
{
    private static readonly IReadOnlyDictionary<string, UserRole> MinimumRole =
        new Dictionary<string, UserRole>(StringComparer.OrdinalIgnoreCase)
        {
            // Maker-checker: an Employee cannot release directly (they request release).
            // Releasing/approving an order requires at least Manager.
            // ApproveOrder and GetPendingApprovals are lowered to Employee here to allow
            // Delegated Employees to access them. The actual functions will enforce Manager or Delegate.
            ["ApproveOrder"] = UserRole.Employee,
            ["ApproveSelectedOrders"] = UserRole.Employee,
            ["GetPendingApprovals"] = UserRole.Employee,
            ["RejectApproval"] = UserRole.Employee,

            ["ReleaseOrder"] = UserRole.Manager,
            ["ReassignOwner"] = UserRole.Manager,

            // Overrides and full audit are Admin-only.
            ["ForceRelease"] = UserRole.Admin,
            ["ForceCancel"] = UserRole.Admin,
            ["ViewAuditLog"] = UserRole.Admin,
            ["ListBotUsers"] = UserRole.Admin,
            ["ManageBotUser"] = UserRole.Admin,
        };

    /// <summary>The minimum role required for a given function (Employee if unlisted).</summary>
    public static UserRole RequiredRole(string functionName) =>
        functionName is not null && MinimumRole.TryGetValue(functionName, out var role)
            ? role
            : UserRole.Employee;

    /// <summary>True when a user with <paramref name="role"/> may execute <paramref name="functionName"/>.</summary>
    public static bool CanExecute(UserRole role, string functionName) =>
        role >= RequiredRole(functionName);
}
