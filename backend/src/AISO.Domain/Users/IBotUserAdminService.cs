namespace AISO.Domain.Users;

/// <summary>Linked bot user snapshot for Admin management.</summary>
public sealed record BotUserSummary(
    string SapUserId,
    string DisplayName,
    UserRole Role,
    string? SalesOrg,
    bool HasLinkAssignment);

/// <summary>Admin ops on bot RBAC (Postgres), not Microsoft 365.</summary>
public interface IBotUserAdminService
{
    Task<IReadOnlyList<BotUserSummary>> ListLinkedUsersAsync(CancellationToken ct = default);

    Task<BotUserSummary?> GetBySapUserIdAsync(string sapUserId, CancellationToken ct = default);

    /// <summary>
    /// Updates role/SalesOrg on <c>user_mappings</c> and matching <c>sap_link_assignments</c>.
    /// </summary>
    Task<BotUserSummary> UpdateAccessAsync(
        string sapUserId,
        UserRole role,
        string? salesOrg,
        CancellationToken ct = default);

    /// <summary>
    /// Pre-assigns a Teams email to an SAP User ID in <c>sap_link_assignments</c>.
    /// Used before the user has linked the bot.
    /// </summary>
    Task<BotUserSummary> PreAssignAccessAsync(
        string sapUserId,
        string teamsEmail,
        UserRole role,
        string? salesOrg,
        CancellationToken ct = default);
}
