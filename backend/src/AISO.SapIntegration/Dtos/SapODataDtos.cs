using System.Text.Json.Serialization;

namespace AISO.SapIntegration.Dtos;

internal class ODataResponse<T>
{
    [JsonPropertyName("value")]
    public List<T>? Value { get; set; }
}

internal class SapValidMaterialPlantDto
{
    public string Material { get; set; } = string.Empty;
    public string Plant { get; set; } = string.Empty;
    public string MaterialType { get; set; } = string.Empty;
    public string BaseUnit { get; set; } = string.Empty;
}

internal class SapValidMaterialSalesDto
{
    public string Material { get; set; } = string.Empty;
    public string SalesOrg { get; set; } = string.Empty;
    public string DistChannel { get; set; } = string.Empty;
    public string Plant { get; set; } = string.Empty;
}

internal class SapSalesAreaDto
{
    public string? SalesOrg { get; set; }
    public string? DistrChannel { get; set; }
    public string? Division { get; set; }
    public string? SalesOrgName { get; set; }
    public string? DistChannelName { get; set; }
    public string? DivisionName { get; set; }
}

internal class SapValidCustomerDto
{
    public string? Customer { get; set; }
    public string? SalesOrg { get; set; }
    public string? DistChannel { get; set; }
    public string? Division { get; set; }
    public string? CustomerName { get; set; }
    public string? Country { get; set; }
}

internal class SapMaterialDto
{
    public string? Material { get; set; }
    public string? MaterialName { get; set; }
    public string? CreatedOn { get; set; }
}

internal class SapSalesOrderDto
{
    public string? SoNumber { get; set; }
    public string? DocType { get; set; }
    public string? Customer { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerReference { get; set; }
    public string? SalesOrg { get; set; }
    public string? DistChannel { get; set; }
    public string? Division { get; set; }
    public string? Currency { get; set; }
    public decimal? NetValue { get; set; }
    public string? DocDate { get; set; }
    public string? RequestedDeliveryDate { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? OverallStatus { get; set; }
    public string? CreditStatus { get; set; }
    public string? DeliveryBlock { get; set; }
    public string? BillingStatus { get; set; }
    public string? IsCancelled { get; set; }
    /// <summary>Owner SAP user from zaiso_so_map (when Quân exposes it on SalesOrder).</summary>
    public string? OwnerSapUser { get; set; }
    /// <summary>X when any item material is missing from MARA; empty otherwise.</summary>
    public string? HasInvalidMaterial { get; set; }
}

/// <summary>
/// Flat SalesOrderItem entity (ZI_AISO_SO_ITEM). Loaded via a separate OData request —
/// the SalesOrder service does not expose an association/$expand for items.
/// </summary>
internal class SapSalesOrderItemDto
{
    public string? SoNumber { get; set; }
    /// <summary>VBAP.POSNR — exposed as ItemNo in CDS.</summary>
    public string? ItemNo { get; set; }
    public string? Material { get; set; }
    /// <summary>MAKT.MAKTX — exposed as MaterialName in CDS.</summary>
    public string? MaterialName { get; set; }
    public string? Plant { get; set; }
    public decimal? OrderQty { get; set; }
    public string? Unit { get; set; }
    public decimal? NetValue { get; set; }
    public string? Currency { get; set; }
    public string? RejectionRsn { get; set; }
}

internal class SapUserRoleDto
{
    public string? SapUser { get; set; }

    /// <summary>CDS element renamed to <c>UserRole</c> (was <c>Role</c>).</summary>
    public string? UserRole { get; set; }

    /// <summary>Legacy element name; kept for older bindings.</summary>
    public string? Role { get; set; }

    public string? SalesOrg { get; set; }
}

// KPI DTOs — field names must match SAP CDS view element names (PascalCase via OData)
internal class SapKpiRevenueDto
{
    public string? SalesOrg { get; set; }
    public string? Currency { get; set; }
    public string? BillingDate { get; set; }
    public decimal? TotalRevenue { get; set; }
    public int? InvoiceCount { get; set; }
}

internal class SapKpiSummaryDto
{
    public string? PeriodLabel { get; set; }
    public decimal? TotalRevenue { get; set; }
    public string? Currency { get; set; }
    public int? OrderCount { get; set; }
    public int? OpenCount { get; set; }
    public int? DeliveredCount { get; set; }
    public int? OverdueCount { get; set; }
    public int? CancelledCount { get; set; }
}

internal class SapKpiByCustomerDto
{
    public string? Customer { get; set; }
    public string? CustomerName { get; set; }
    public decimal? TotalRevenue { get; set; }
    public string? Currency { get; set; }
    public int? OrderCount { get; set; }
    public decimal? FulfillmentRate { get; set; }
}

internal class SapKpiByProductDto
{
    public string? Material { get; set; }
    public string? MaterialName { get; set; }
    public decimal? TotalRevenue { get; set; }
    public string? Currency { get; set; }
    public decimal? TotalQty { get; set; }
    public string? Unit { get; set; }
    public int? OrderCount { get; set; }
}

internal class SapOverdueOrderDto
{
    public string? SoNumber { get; set; }
    public string? Customer { get; set; }
    public string? CustomerName { get; set; }
    public string? SalesOrg { get; set; }
    public string? ScheduledDeliveryDate { get; set; }
    public int? DaysPastDue { get; set; }
    public decimal? NetValue { get; set; }
    public string? Currency { get; set; }
}
