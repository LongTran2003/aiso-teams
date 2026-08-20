namespace AISO.Domain.SalesOrders;

/// <summary>
/// Business rules for which lifecycle actions remain allowed on a sales order.
/// </summary>
public static class SalesOrderWorkflow
{
    /// <summary>
    /// Delivery started/finished or order cancelled — release / forward no longer apply.
    /// </summary>
    public static bool BlocksReleaseRejectForward(SalesOrderStatus status) =>
        status is SalesOrderStatus.PartiallyDelivered
            or SalesOrderStatus.Delivered
            or SalesOrderStatus.Cancelled;

    /// <summary>
    /// Reject is blocked after delivery has started, and when already cancelled.
    /// Avoids cryptic SAP item-change errors (e.g. "material cannot be changed") on delivered SOs.
    /// </summary>
    public static bool BlocksReject(SalesOrderStatus status) =>
        BlocksReleaseRejectForward(status);

    public static string BuildBlockedMessage(SalesOrderStatus status, string actionLabel)
    {
        if (status == SalesOrderStatus.Cancelled)
        {
            return $"Sales order is Cancelled. {actionLabel} is not allowed on a rejected order.";
        }

        var statusLabel = status switch
        {
            SalesOrderStatus.PartiallyDelivered => "Partially Delivered",
            SalesOrderStatus.Delivered => "Delivered",
            _ => status.ToString()
        };

        return $"Sales order is already {statusLabel}. {actionLabel} is not allowed after delivery has started.";
    }

    /// <summary>
    /// Soft-lock while a release request is waiting for Manager decision.
    /// Blocks request-release / reject / forward; ApproveOrder and RejectApproval remain allowed.
    /// </summary>
    public static string BuildPendingApprovalBlockedMessage(string actionLabel, string? requestedBySapUser = null)
    {
        var by = string.IsNullOrWhiteSpace(requestedBySapUser)
            ? string.Empty
            : $" (submitted by {requestedBySapUser.Trim()})";

        return $"Sales order already has a pending release request{by}. " +
               $"{actionLabel} is not allowed until a Manager approves or rejects the request.";
    }

    /// <summary>
    /// Empty owner matches SAP: no <c>zaiso_so_map</c> row means any linked user may act.
    /// </summary>
    public static bool IsCurrentOwner(string? ownerSapUser, string? currentSapUser)
    {
        if (string.IsNullOrWhiteSpace(ownerSapUser))
            return false;

        if (string.IsNullOrWhiteSpace(currentSapUser))
            return false;

        return string.Equals(ownerSapUser.Trim(), currentSapUser.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildNotOwnerBlockedMessage(string actionLabel, string ownerSapUser) =>
        $"Sales order is owned by {ownerSapUser.Trim()}. {actionLabel} is only allowed for the current owner.";

    /// <summary>
    /// SO has item material(s) missing from MARA — release/reject/forward will fail in SAP.
    /// </summary>
    public static string BuildInvalidMaterialBlockedMessage(string actionLabel) =>
        $"{actionLabel} is not allowed: this sales order has invalid material master data.";

    /// <summary>
    /// Pending release UI only makes sense while the order can still be released.
    /// Stale Postgres pending on Delivered/Cancelled/etc. should not show "Waiting for approval".
    /// </summary>
    public static bool ShowsPendingApprovalBanner(SalesOrderStatus status) =>
        status is SalesOrderStatus.Open or SalesOrderStatus.Blocked;
}
