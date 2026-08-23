using System.Text.Json;
using AISO.Domain.SalesOrders;
using AISO.Domain.Users;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Returns the requesting user's profile snapshot:
/// identity (SAP user, role, sales org) + order-status breakdown + top recent orders.
///
/// Triggered by "my profile" / "há»“ sÆ¡ cá»§a tÃ´i" / "thÃ´ng tin cá»§a tÃ´i" shortcuts.
/// Sales org is read from <see cref="IUserScopeLookup"/> (Postgres fallback) so the
/// command works without depending on the new SAP <c>ZI_AISO_USER_ROLE</c> GET endpoint
/// that the SAP team is still exposing.
/// </summary>
public sealed class MyProfileFunction : IFunction
{
    /// <summary>How many orders to fetch for counting + top list. 200 is the cap we accept today;
    /// counts are labelled "approximate" on the card when this many orders is reached.</summary>
    public const int MaxOrdersForStats = 200;

    private readonly ISapClient _sap;
    private readonly IUserScopeLookup _scopeLookup;
    private readonly ILogger<MyProfileFunction> _logger;

    public MyProfileFunction(
        ISapClient sap,
        IUserScopeLookup scopeLookup,
        ILogger<MyProfileFunction> logger)
    {
        _sap = sap;
        _scopeLookup = scopeLookup;
        _logger = logger;
    }

    public string Name => "MyProfile";

    public string Description =>
        "Returns the current user's profile: SAP user id, display name, role, sales org, " +
        "and an order-status breakdown (Total/Open/Blocked/PartiallyDelivered/Delivered/Invoiced/Cancelled) " +
        "plus the 5 most recent sales orders they own. Use for 'my profile' / 'há»“ sÆ¡ cá»§a tÃ´i'.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(
        JsonElement parameters,
        string requestingSapUser,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(requestingSapUser))
        {
            return FunctionResult.Fail(
                "Your Teams account is not linked to a SAP user yet. " +
                "Say \"hi\" or \"link\" to connect your SAP account, then retry.",
                errorCode: "NOT_LINKED");
        }

        _logger.LogInformation("Executing MyProfile for {SapUser}", requestingSapUser);

        // Identity (Postgres fallback for sales org, until SAP exposes ZI_AISO_USER_ROLE GET).
        var role = await _scopeLookup.GetRoleBySapUserAsync(requestingSapUser, ct);
        var salesOrg = await _scopeLookup.GetSalesOrgBySapUserAsync(requestingSapUser, ct);

        // Orders owned by the current user (top=200 for approximate stats).
        var query = new SalesOrdersQuery
        {
            OwnerSapUser = requestingSapUser,
            Top = MaxOrdersForStats,
        };

        IReadOnlyList<SalesOrder> orders;
        try
        {
            orders = await _sap.GetSalesOrdersAsync(query, ct);
        }
        catch (SapODataException ex) when (ex.HttpStatusCode >= 500 || ex.HttpStatusCode == 400)
        {
            _logger.LogWarning(ex, "SAP error while loading own orders for {SapUser}", requestingSapUser);
            // Build a partial response so the user still sees their identity + role + sales org.
            return FunctionResult.Ok(new MyProfileResponse(
                SapUser: requestingSapUser,
                Role: role,
                SalesOrg: salesOrg,
                Counts: MyProfileOrderCounts.Empty,
                Approximate: false,
                TopOrders: Array.Empty<SalesOrder>(),
                SalesOrgSource: MyProfileSalesOrgSource.Postgres,
                LoadError: $"Could not load your orders: {ex.Message}"));
        }

        var counts = MyProfileOrderCounts.From(orders);
        var approximate = orders.Count >= MaxOrdersForStats;

        // Top 5 by order date desc, then SoNumber desc as tie-breaker for stable display.
        var top = orders
            .OrderByDescending(o => o.OrderDate)
            .ThenByDescending(o => o.SoNumber, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        var response = new MyProfileResponse(
            SapUser: requestingSapUser,
            Role: role,
            SalesOrg: salesOrg,
            Counts: counts,
            Approximate: approximate,
            TopOrders: top,
            SalesOrgSource: MyProfileSalesOrgSource.Postgres,
            LoadError: null);

        _logger.LogInformation(
            "MyProfile for {SapUser}: total {Total}, open {Open}, topReturned {Top}",
            requestingSapUser, counts.Total, counts.Open, top.Count);

        return FunctionResult.Ok(response);
    }
}

/// <summary>
/// Snapshot for the "my profile" Adaptive Card. All counts are taken from a single
/// <see cref="SalesOrdersQuery"/>; <see cref="Approximate"/> is true when the query
/// hit the <c>MaxOrdersForStats</c> ceiling (the user owns more than the cap).
/// </summary>
public sealed record MyProfileResponse(
    string SapUser,
    UserRole Role,
    string? SalesOrg,
    MyProfileOrderCounts Counts,
    bool Approximate,
    IReadOnlyList<SalesOrder> TopOrders,
    MyProfileSalesOrgSource SalesOrgSource,
    string? LoadError);

/// <summary>Counts grouped by status, plus the grand total.</summary>
public sealed record MyProfileOrderCounts(
    int Total,
    int Open,
    int Blocked,
    int PartiallyDelivered,
    int Delivered,
    int Invoiced,
    int Cancelled)
{
    public static readonly MyProfileOrderCounts Empty = new(0, 0, 0, 0, 0, 0, 0);

    public static MyProfileOrderCounts From(IEnumerable<SalesOrder> orders)
    {
        var list = orders as IReadOnlyCollection<SalesOrder> ?? orders.ToList();
        return new MyProfileOrderCounts(
            Total: list.Count,
            Open: list.Count(o => o.Status == SalesOrderStatus.Open),
            Blocked: list.Count(o => o.Status == SalesOrderStatus.Blocked),
            PartiallyDelivered: list.Count(o => o.Status == SalesOrderStatus.PartiallyDelivered),
            Delivered: list.Count(o => o.Status == SalesOrderStatus.Delivered),
            Invoiced: list.Count(o => o.Status == SalesOrderStatus.Invoiced),
            Cancelled: list.Count(o => o.Status == SalesOrderStatus.Cancelled));
    }
}

/// <summary>
/// Where the sales org value came from. Today only <c>Postgres</c>; will add
/// <c>SapUserRole</c> once the SAP team exposes ZI_AISO_USER_ROLE GET.
/// </summary>
public enum MyProfileSalesOrgSource
{
    Postgres = 0,
    SapUserRole = 1,
}
