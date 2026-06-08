namespace AISO.Domain.SalesOrders;

/// <summary>
/// Derived Sales Order status used for display and filtering.
/// Computed in the SAP integration layer from VBAK delivery/billing/block fields.
/// </summary>
public enum SalesOrderStatus
{
    Open,
    Blocked,
    PartiallyDelivered,
    Delivered,
    Invoiced,
    Cancelled
}
