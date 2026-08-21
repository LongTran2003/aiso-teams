using AISO.Domain.Kpi;
using AISO.Domain.SalesOrders;
using AISO.Domain.Approvals;

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

    /// <summary>
    /// Admin: upsert <c>ZAISO_USER_ROLE</c> via OData <c>UserRole.syncUserRole</c>.
    /// </summary>
    Task SyncUserRoleAsync(
        string targetSapUser,
        string newRole,
        string? salesOrg,
        string requestingAdminSapUser,
        CancellationToken ct = default);

    Task<SalesOrder> UpdateReferenceAsync(
        string soNumber,
        string newReference,
        string requestingSapUser,
        CancellationToken ct = default);

    /// <summary>
    /// Full edit via SAP <c>updateSalesOrder</c> (header PO/date + line I/U/D).
    /// </summary>
    Task<SalesOrder> UpdateSalesOrderAsync(
        UpdateSalesOrderDto dto,
        CancellationToken ct = default);

    /// <summary>
    /// Rejects a sales order through the SAP <c>rejectOrder</c> RAP action.
    /// </summary>
    Task<SalesOrder> RejectOrderAsync(
        string soNumber,
        string rejectionCode,
        string requestingTeamsUser,
        CancellationToken ct = default);

    /// <summary>
    /// Cancels a sales order via SAP <c>cancelOrder</c>.
    /// Employee: own SO only (SAP + BE). Manager/Admin: any SO.
    /// </summary>
    Task<SalesOrder> CancelOrderAsync(
        string soNumber,
        string requestingSapUser,
        string? reason = null,
        CancellationToken ct = default);

    /// <summary>
    /// Delegates approval authority to another SAP user via SAP <c>delegateApproval</c>.
    /// </summary>
    Task DelegateApprovalAsync(
        DelegateApprovalDto dto,
        CancellationToken ct = default);

    /// <summary>
    /// Revokes an existing approval delegation via SAP <c>revokeDelegation</c>.
    /// </summary>
    Task RevokeDelegationAsync(
        RevokeDelegationDto dto,
        CancellationToken ct = default);

    Task<SalesOrder> ReleaseOrderAsync(
        string soNumber,
        string requestingTeamsUser,
        CancellationToken ct = default);

    /// <summary>
    /// Phase A: Manager/Admin approve via SAP <c>approveOrder</c>
    /// (role from <c>ZAISO_USER_ROLE</c> + real release in saver).
    /// </summary>
    Task<SalesOrder> ApproveOrderAsync(
        string soNumber,
        string requestingSapUser,
        CancellationToken ct = default);

    /// <summary>Phase A: Manager/Admin SAP-side rejectApproval audit (no SO reject).</summary>
    Task<SalesOrder> RejectApprovalAsync(
        string soNumber,
        string requestingSapUser,
        CancellationToken ct = default);

    /// <summary>Phase A: Admin forceRelease (bypasses ownership).</summary>
    Task<SalesOrder> ForceReleaseAsync(
        string soNumber,
        string requestingSapUser,
        string overrideReason,
        CancellationToken ct = default);

    /// <summary>Phase A: Admin forceCancel (bypasses ownership).</summary>
    Task<SalesOrder> ForceCancelAsync(
        string soNumber,
        string requestingSapUser,
        string overrideReason,
        CancellationToken ct = default);

    /// <summary>Phase A: Manager/Admin reassign SO owner in zaiso_so_map.</summary>
    Task<SalesOrder> ReassignOwnerAsync(
        string soNumber,
        string newOwnerSapUser,
        string requestingSapUser,
        CancellationToken ct = default);

    Task<SalesOrder> ForwardOrderAsync(
        string soNumber,
        string forwardToUser,
        string requestingTeamsUser,
        CancellationToken ct = default,
        string? remarks = null);

    // -----------------------------------------------------------------------
    // KPI methods (Sprint 4)
    // -----------------------------------------------------------------------

    /// <summary>Get aggregated KPI dashboard: revenue totals, order counts, fulfillment rate.</summary>
    Task<KpiSummary> GetKpiSummaryAsync(KpiSummaryQuery query, CancellationToken ct = default);

    /// <summary>Get KPI breakdown per customer, ordered by revenue descending.</summary>
    Task<IReadOnlyList<KpiByCustomer>> GetKpiByCustomerAsync(KpiByCustomerQuery query, CancellationToken ct = default);

    /// <summary>Get KPI breakdown per product/material, ordered by revenue descending.</summary>
    Task<IReadOnlyList<KpiByProduct>> GetKpiByProductAsync(KpiByProductQuery query, CancellationToken ct = default);

    /// <summary>Get sales orders that have exceeded their scheduled delivery date.</summary>
    Task<IReadOnlyList<OverdueOrder>> GetOverdueOrdersAsync(OverdueOrdersQuery query, CancellationToken ct = default);

    /// <summary>
    /// Returns whether <paramref name="sapUserId"/> exists in SAP AISO user-role master
    /// (<c>UserRole</c> / <c>ZAISO_USER_ROLE</c>). Null means the lookup was unavailable.
    /// </summary>
    Task<bool?> SapUserExistsAsync(string sapUserId, CancellationToken ct = default);

    /// <summary>Valid sales areas from <c>SalesArea</c> (TVTA).</summary>
    Task<IReadOnlyList<SapSalesArea>> GetSalesAreasAsync(CancellationToken ct = default);

    /// <summary>Material master from <c>Material</c>.</summary>
    Task<IReadOnlyList<SapMaterial>> GetMaterialsAsync(CancellationToken ct = default);

    /// <summary>Valid Material per Plant from <c>ValidMaterialPlant</c>.</summary>
    Task<IReadOnlyList<SapValidMaterialPlant>> GetValidMaterialPlantsAsync(CancellationToken ct = default);

    /// <summary>Valid Materials for a specific Sales Org and Dist Channel.</summary>
    Task<IReadOnlyList<SapValidMaterialSales>> GetValidMaterialSalesAsync(
        string? salesOrg = null,
        string? distChannel = null,
        int top = 30,
        CancellationToken ct = default);

    /// <summary>Customers valid per sales area from <c>ValidCustomer</c> (KNVV).</summary>
    Task<IReadOnlyList<SapValidCustomer>> GetValidCustomersAsync(
        string? salesOrg = null,
        string? distChannel = null,
        string? division = null,
        int top = 30,
        CancellationToken ct = default);

    /// <summary>
    /// Whether customer is maintained for the sales area.
    /// <c>null</c> = lookup unavailable (caller may proceed).
    /// </summary>
    Task<bool?> IsCustomerValidForSalesAreaAsync(
        string customer,
        string salesOrg,
        string distChannel,
        string division,
        CancellationToken ct = default);
}

/// <summary>OData <c>SalesArea</c> row (VKORG / VTWEG / SPART).</summary>
public sealed record SapSalesArea(string SalesOrg, string DistChannel, string Division)
{
    public string Key => $"{SalesOrg}|{DistChannel}|{Division}";
    public string Label => $"{SalesOrg} / {DistChannel} / {Division}";

    public static bool TryParseKey(string? key, out string salesOrg, out string distChannel, out string division)
    {
        salesOrg = distChannel = division = string.Empty;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var parts = key.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
            return false;

        salesOrg = parts[0].ToUpperInvariant();
        distChannel = parts[1].ToUpperInvariant();
        division = parts[2].ToUpperInvariant();
        return true;
    }
}

/// <summary>OData <c>ValidCustomer</c> row (KNVV + name).</summary>
public sealed record SapValidCustomer(
    string Customer,
    string SalesOrg,
    string DistChannel,
    string Division,
    string? CustomerName = null)
{
    /// <summary>Choice value: Customer|SalesOrg|DistChannel|Division.</summary>
    public string Key => $"{Customer.Trim()}|{SalesOrg}|{DistChannel}|{Division}";

    public string Label
    {
        get
        {
            var id = Customer.TrimStart('0');
            if (string.IsNullOrEmpty(id))
                id = Customer;
            var name = string.IsNullOrWhiteSpace(CustomerName) ? null : CustomerName.Trim();
            var area = $"{SalesOrg}/{DistChannel}/{Division}";
            return name is null ? $"{id} ({area})" : $"{id} · {name} ({area})";
        }
    }

    public static bool TryParseKey(
        string? key,
        out string customer,
        out string salesOrg,
        out string distChannel,
        out string division)
    {
        customer = salesOrg = distChannel = division = string.Empty;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var parts = key.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
            return false;

        customer = parts[0];
        salesOrg = parts[1].ToUpperInvariant();
        distChannel = parts[2].ToUpperInvariant();
        division = parts[3].ToUpperInvariant();
        return !string.IsNullOrWhiteSpace(customer);
    }
}

/// <summary>OData <c>Material</c> row.</summary>
public sealed record SapMaterial(
    string Material,
    string MaterialName,
    DateTimeOffset? CreatedOn)
{
    public string Label => $"{Material} - {MaterialName}";
}

/// <summary>OData <c>ValidMaterialPlant</c> row.</summary>
public sealed record SapValidMaterialPlant(
    string Material,
    string Plant,
    string MaterialType,
    string BaseUnit);

/// <summary>OData <c>ValidMaterialSales</c> row.</summary>
public sealed record SapValidMaterialSales(
    string Material,
    string SalesOrg,
    string DistrChannel);

public sealed record CreateSalesOrderDto
{
    public required string DocType { get; init; }
    public required string SalesOrg { get; init; }
    public required string DistChannel { get; init; }
    public required string Division { get; init; }
    public required string Customer { get; init; }
    public required string Currency { get; init; }
    public required IReadOnlyList<CreateSalesOrderItemDto> Items { get; init; }

    /// <summary>SAP user id of the creator (OData <c>REQUESTING_TEAMS_USER</c>).</summary>
    public string? RequestingSapUser { get; init; }
}

public sealed record CreateSalesOrderItemDto
{
    public required string Material { get; init; }
    public required string Plant { get; init; }
    public required decimal OrderQty { get; init; }
    public required string Unit { get; init; }
}

/// <summary>Payload for SAP <c>updateSalesOrder</c> (ZAISO_A_UPDATE_SO: NEW_REFERENCE, REQUESTED_DELIVERY_DATE, ITEMS).</summary>
public sealed record UpdateSalesOrderDto
{
    public required string SoNumber { get; init; }
    public required string RequestingSapUser { get; init; }
    /// <summary>Maps to NEW_REFERENCE; omit/empty to leave PO reference unchanged.</summary>
    public string? PurchaseOrderRef { get; init; }
    /// <summary>yyyy-MM-dd maps to REQUESTED_DELIVERY_DATE; omit/empty to leave unchanged.</summary>
    public string? ReqDeliveryDate { get; init; }
    public IReadOnlyList<UpdateSalesOrderItemDto> Items { get; init; } = [];
}

public sealed record UpdateSalesOrderItemDto
{
    /// <summary>I = insert, U = update, D = delete (CHANGE_FLAG).</summary>
    public required string Operation { get; init; }
    public string? ItemNumber { get; init; }
    public string? Material { get; init; }
    /// <summary>Not sent on update OData yet (ZAISO_S_SO_ITEM_UPDATE has no PLANT).</summary>
    public string? Plant { get; init; }
    public decimal? OrderQty { get; init; }
    public string? Unit { get; init; }
}

/// <summary>
/// Filter criteria for SalesOrder queries. All properties are optional;
/// nulls mean "no filter".
/// </summary>
public sealed record SalesOrdersQuery
{
    /// <summary>
    /// Exact customer ID → OData <c>Customer eq</c>;
    /// name / partial name → <c>contains(CustomerName,'…')</c>.
    /// </summary>
    public string? CustomerIdOrName { get; init; }

    /// <summary>Sales organization code (TV01, FU24, UE00, UW00, DN00, DS00).</summary>
    public string? SalesOrg { get; init; }

    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public SalesOrderStatus? Status { get; init; }

    /// <summary>
    /// When set, OData filters <c>OwnerSapUser eq '{value}'</c> (orders owned by that SAP user).
    /// Used for "my sales orders" / "đơn của tôi".
    /// </summary>
    public string? OwnerSapUser { get; init; }

    /// <summary>
    /// When true (default), OData filters <c>HasInvalidMaterial eq ''</c> so list/KPI
    /// paths exclude SOs with missing material master data.
    /// Set false only for admin/debug listing of dirty orders.
    /// </summary>
    public bool ExcludeInvalidMaterials { get; init; } = true;

    /// <summary>Maximum number of records to return (default 10, max 50).</summary>
    public int Top { get; init; } = 10;
}
