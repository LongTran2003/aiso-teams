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
}
