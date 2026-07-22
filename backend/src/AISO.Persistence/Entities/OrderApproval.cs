using AISO.Domain.Approvals;

namespace AISO.Persistence.Entities;

/// <summary>
/// Persists a maker-checker release request for a sales order.
/// </summary>
public class OrderApproval
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Padded SAP sales order number.</summary>
    public string SoNumber { get; set; } = string.Empty;

    /// <summary>SAP user ID of the employee who requested release.</summary>
    public string RequestedBySapUser { get; set; } = string.Empty;

    /// <summary>Sales organization (VKORG) of the order, used for Manager scoping.</summary>
    public string? SalesOrg { get; set; }

    public string? Comment { get; set; }

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    public string? DecidedBySapUser { get; set; }

    public string? DecisionComment { get; set; }

    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? DecidedAt { get; set; }
}
