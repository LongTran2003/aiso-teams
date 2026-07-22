namespace AISO.Domain.Users;

/// <summary>
/// Resolves RBAC scope (role + VKORG) for a SAP user.
/// Used by singleton AI functions that cannot take a scoped UserMappingService.
/// </summary>
public interface IUserScopeLookup
{
    Task<UserRole> GetRoleBySapUserAsync(string sapUserId, CancellationToken ct = default);

    Task<string?> GetSalesOrgBySapUserAsync(string sapUserId, CancellationToken ct = default);
}
