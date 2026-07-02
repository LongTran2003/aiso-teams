using System.Text.Json.Serialization;

namespace AISO.SapIntegration.Dtos;

internal class ODataResponse<T>
{
    [JsonPropertyName("value")]
    public List<T>? Value { get; set; }
}

internal class SapSalesOrderDto
{
    public string? SoNumber { get; set; }
    public string? DocType { get; set; }
    public string? Customer { get; set; }
    public string? SalesOrg { get; set; }
    public string? DistChannel { get; set; }
    public string? Division { get; set; }
    public string? Currency { get; set; }
    public decimal? NetValue { get; set; }
    public string? DocDate { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? OverallStatus { get; set; }
}

// KPI DTOs — field names must match SAP CDS view element names (PascalCase via OData)
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
