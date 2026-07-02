namespace AISO.Domain.Kpi;

/// <summary>
/// Overall KPI dashboard: aggregated totals across all dimensions.
/// Returned by GetKpiSummary.
/// </summary>
public sealed record KpiSummary
{
    public decimal TotalRevenue { get; init; }
    public string Currency { get; init; } = "USD";
    public int TotalOrders { get; init; }
    public int OpenOrders { get; init; }
    public int DeliveredOrders { get; init; }
    public int OverdueOrders { get; init; }
    public decimal FulfillmentRate { get; init; }   // 0–100 %
    public decimal CancellationRate { get; init; }  // 0–100 %
    public string? Period { get; init; }
    public string? SalesOrg { get; init; }
    public string? Granularity { get; init; }
    public IReadOnlyList<KpiDataPoint> RevenueTimeSeries { get; init; } = [];
}

/// <summary>A single data point in a time series (e.g. one week's revenue).</summary>
public sealed record KpiDataPoint(string Label, decimal Value);

/// <summary>KPI breakdown per customer. Returned by GetKpiByCustomer.</summary>
public sealed record KpiByCustomer
{
    public string CustomerId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public decimal Revenue { get; init; }
    public string Currency { get; init; } = "USD";
    public int OrderCount { get; init; }
    public decimal FulfillmentRate { get; init; }
}

/// <summary>KPI breakdown per material/product. Returned by GetKpiByProduct.</summary>
public sealed record KpiByProduct
{
    public string MaterialId { get; init; } = string.Empty;
    public string MaterialName { get; init; } = string.Empty;
    public decimal Revenue { get; init; }
    public string Currency { get; init; } = "USD";
    public decimal TotalQty { get; init; }
    public string Unit { get; init; } = "PC";
    public int OrderCount { get; init; }
}

/// <summary>A sales order that has exceeded its scheduled delivery date.</summary>
public sealed record OverdueOrder
{
    public string SoNumber { get; init; } = string.Empty;
    public string CustomerId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public DateOnly ScheduledDeliveryDate { get; init; }
    public int DaysPastDue { get; init; }
    public decimal NetValue { get; init; }
    public string Currency { get; init; } = "USD";
    public string SalesOrg { get; init; } = string.Empty;
}

// ---------------------------------------------------------------------------
// Query DTOs
// ---------------------------------------------------------------------------

/// <summary>Filter criteria for KPI Summary query. All properties optional.</summary>
public sealed record KpiSummaryQuery
{
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public string? SalesOrg { get; init; }
    public string? Granularity { get; init; }  // "daily" | "weekly" | "monthly"
}

/// <summary>Filter criteria for KPI By Customer query.</summary>
public sealed record KpiByCustomerQuery
{
    public string? CustomerIdOrName { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public string? SalesOrg { get; init; }
    public int Top { get; init; } = 10;
}

/// <summary>Filter criteria for KPI By Product query.</summary>
public sealed record KpiByProductQuery
{
    public string? MaterialIdOrName { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public string? SalesOrg { get; init; }
    public int Top { get; init; } = 10;
}

/// <summary>Filter criteria for Overdue Orders query.</summary>
public sealed record OverdueOrdersQuery
{
    public string? CustomerIdOrName { get; init; }
    public string? SalesOrg { get; init; }
    public int? DaysPastDue { get; init; }
    public int Top { get; init; } = 20;
}
