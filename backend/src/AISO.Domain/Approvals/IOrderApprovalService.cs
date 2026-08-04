namespace AISO.Domain.Approvals;

/// <summary>
/// Phase B maker-checker store: Employees submit release requests;
/// Managers approve/reject within their sales organization.
/// </summary>
public interface IOrderApprovalService
{
    Task<OrderApprovalRequest> RequestReleaseAsync(
        string soNumber,
        string requestedBySapUser,
        string? salesOrg,
        string? comment,
        CancellationToken ct = default);

    Task<OrderApprovalRequest?> GetPendingBySoNumberAsync(
        string soNumber,
        CancellationToken ct = default);

    /// <summary>
    /// Latest approval row for an SO (pending or decided), for journey/timeline UI.
    /// </summary>
    Task<OrderApprovalRequest?> GetLatestBySoNumberAsync(
        string soNumber,
        CancellationToken ct = default);

    Task<IReadOnlyList<OrderApprovalRequest>> GetPendingAsync(
        string? salesOrgFilter,
        CancellationToken ct = default);

    Task<OrderApprovalRequest> ApproveAsync(
        string soNumber,
        string decidedBySapUser,
        string? managerSalesOrg,
        bool isAdmin,
        string? comment,
        CancellationToken ct = default);

    Task<OrderApprovalRequest> RejectAsync(
        string soNumber,
        string decidedBySapUser,
        string? managerSalesOrg,
        bool isAdmin,
        string? comment,
        CancellationToken ct = default);
}

/// <summary>Snapshot of a release-approval request.</summary>
public sealed record OrderApprovalRequest
{
    public required Guid Id { get; init; }
    public required string SoNumber { get; init; }
    public required string RequestedBySapUser { get; init; }
    public string? SalesOrg { get; init; }
    public string? Comment { get; init; }
    public required ApprovalStatus Status { get; init; }
    public string? DecidedBySapUser { get; init; }
    public string? DecisionComment { get; init; }
    public required DateTimeOffset RequestedAt { get; init; }
    public DateTimeOffset? DecidedAt { get; init; }
}
