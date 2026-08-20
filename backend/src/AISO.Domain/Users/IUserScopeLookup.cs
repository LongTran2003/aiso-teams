namespace AISO.Domain.Users;

public record DelegationInfo(string? DelegatorSapUser, decimal? MaxAmount);

/// <summary>
/// Resolves RBAC scope (role + VKORG) for a SAP user.
/// Used by singleton AI functions that cannot take a scoped UserMappingService.
/// </summary>
public interface IUserScopeLookup
{
    Task<UserRole> GetRoleBySapUserAsync(string sapUserId, CancellationToken ct = default);

    Task<string?> GetSalesOrgBySapUserAsync(string sapUserId, CancellationToken ct = default);

    Task<string?> GetDelegatedBySapUserAsync(string sapUserId, CancellationToken ct = default);

    Task<string?> GetEmailBySapUserAsync(string sapUserId, CancellationToken ct = default);

    Task<DelegationInfo> GetDelegationInfoAsync(string sapUserId, CancellationToken ct = default);

    Task SetDelegatedBySapUserAsync(string delegateUser, string? delegatorUser, DateTimeOffset? validTo = null, decimal? maxAmount = null, CancellationToken ct = default);

    Task<IReadOnlyList<ActiveDelegation>> GetActiveDelegationsAsync(string? filterDelegatorUser = null, CancellationToken ct = default);
}

public record ActiveDelegation(
    string DelegateUser,
    string DelegateName,
    string DelegatorUser,
    DateTimeOffset? ValidTo,
    decimal? MaxAmount);
