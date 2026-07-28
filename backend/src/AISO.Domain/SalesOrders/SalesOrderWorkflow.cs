namespace AISO.Domain.SalesOrders;

/// <summary>
/// Business rules for which lifecycle actions remain allowed on a sales order.
/// </summary>
public static class SalesOrderWorkflow
{
    /// <summary>
    /// Delivery has started or finished — release / reject / forward (and request-release) no longer apply.
    /// </summary>
    public static bool BlocksReleaseRejectForward(SalesOrderStatus status) =>
        status is SalesOrderStatus.PartiallyDelivered or SalesOrderStatus.Delivered;

    public static string BuildBlockedMessage(SalesOrderStatus status, string actionLabel)
    {
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
}
