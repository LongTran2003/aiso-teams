using AISO.Domain.SalesOrders;

namespace AISO.SapIntegration;

/// <summary>
/// Abstraction over the SAP backend. Implementations include MockSapClient
/// (development) and SapClient (HTTP/OData via Cloud Connector, Sprint 3+).
/// </summary>
public interface ISapClient
{
    Task<IReadOnlyList<SalesOrder>> GetSalesOrdersAsync(
        SalesOrdersQuery query,
        CancellationToken ct = default);

    Task<SalesOrder?> GetSalesOrderByIdAsync(
        string soNumber,
        CancellationToken ct = default);

    Task<SalesOrder> CreateSalesOrderAsync(
        CreateSalesOrderDto dto,
        CancellationToken ct = default);

    Task<SalesOrder> UpdateReferenceAsync(
        string soNumber,
        string newReference,
        string requestingSapUser,
        CancellationToken ct = default);

    Task<SalesOrder> CancelOrderAsync(
        string soNumber,
        string reason,
        string requestingSapUser,
        CancellationToken ct = default);
}

public sealed record CreateSalesOrderDto
{
    public required string DocType { get; init; }
    public required string SalesOrg { get; init; }
    public required string DistChannel { get; init; }
    public required string Division { get; init; }
    public required string Customer { get; init; }
    public required string Currency { get; init; }
    public required IReadOnlyList<CreateSalesOrderItemDto> Items { get; init; }
}

public sealed record CreateSalesOrderItemDto
{
    public required string Material { get; init; }
    public required string Plant { get; init; }
    public required decimal OrderQty { get; init; }
    public required string Unit { get; init; }
}

/// <summary>
/// Filter criteria for SalesOrder queries. All properties are optional;
/// nulls mean "no filter".
/// </summary>
public sealed record SalesOrdersQuery
{
    /// <summary>Either an exact customer ID (e.g. "1000") or a partial name match (e.g. "Philly").</summary>
    public string? CustomerIdOrName { get; init; }

    /// <summary>Sales organization code (UE00, UW00, DN00, DS00).</summary>
    public string? SalesOrg { get; init; }

    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public SalesOrderStatus? Status { get; init; }

    /// <summary>Maximum number of records to return (default 10, max 50).</summary>
    public int Top { get; init; } = 10;
}
