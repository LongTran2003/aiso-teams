namespace AISO.Domain.SalesOrders;

/// <summary>
/// Represents a SAP Sales Order (VBAK header + VBAP items).
/// </summary>
public sealed record SalesOrder
{
    /// <summary>SAP Sales Order number (VBELN).</summary>
    public required string SoNumber { get; init; }

    /// <summary>Customer number (KUNNR / VBAK.KUNNR).</summary>
    public required string CustomerId { get; init; }

    /// <summary>Customer display name (KNA1.NAME1).</summary>
    public required string CustomerName { get; init; }

    /// <summary>Order entry date (VBAK.AUDAT).</summary>
    public required DateOnly OrderDate { get; init; }

    /// <summary>Net order value at header level (VBAK.NETWR).</summary>
    public required decimal NetValue { get; init; }

    /// <summary>Currency code (VBAK.WAERK).</summary>
    public required string Currency { get; init; }

    /// <summary>Sales organization code (VBAK.VKORG).</summary>
    public required string SalesOrg { get; init; }

    /// <summary>Derived order status (computed from delivery/billing/block fields).</summary>
    public required SalesOrderStatus Status { get; init; }

    /// <summary>Order line items (VBAP rows).</summary>
    public required IReadOnlyList<SalesOrderItem> Items { get; init; }
}

/// <summary>
/// Represents a single line item of a Sales Order (VBAP row).
/// </summary>
public sealed record SalesOrderItem
{
    /// <summary>Line item number (VBAP.POSNR).</summary>
    public required string ItemNumber { get; init; }

    /// <summary>Material number (VBAP.MATNR).</summary>
    public required string Material { get; init; }

    /// <summary>Material short text (MAKT.MAKTX).</summary>
    public required string Description { get; init; }

    /// <summary>Order quantity (VBAP.KWMENG).</summary>
    public required decimal Quantity { get; init; }

    /// <summary>Sales unit of measure (VBAP.VRKME).</summary>
    public required string Unit { get; init; }

    /// <summary>Item net value (VBAP.NETWR).</summary>
    public required decimal NetValue { get; init; }
}