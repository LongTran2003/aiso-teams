namespace AISO.Domain.Approvals;

/// <summary>Lifecycle of a maker-checker release request.</summary>
public enum ApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
