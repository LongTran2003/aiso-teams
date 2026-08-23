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

        // Identity: try SAP (ZAISO_USER_ROLE) first via ISapClient.GetUserRoleAsync,
        // fall back to Postgres user_mappings when SAP has no row, is unreachable,
        // or returns an error. The fallback chain keeps the bot usable even when
        // the new ZC_AISO_USER_ROLE_QUERY view is not yet activated in DEV.
        var identity = await ResolveIdentityAsync(requestingSapUser, ct);

        // Email comes from Postgres (SapLinkAssignments.TeamsEmail) which is
        // synced from Microsoft Entra ID. SAP does not carry email today.
        // Read in parallel with the orders query below to avoid paying the cost
        // twice when the SAP orders request is slow.
        var emailTask = _scopeLookup.GetEmailBySapUserAsync(requestingSapUser, ct);

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
            // Build a partial response so the user still sees their identity.
            var partialEmail = await SafeGetEmailAsync(emailTask, requestingSapUser, ct);
            return FunctionResult.Ok(new MyProfileResponse(
                SapUser: requestingSapUser,
                Role: identity.Role,
                SalesOrg: identity.SalesOrg,
                Email: partialEmail,
                Counts: MyProfileOrderCounts.Empty,
                Approximate: false,
                TopOrders: Array.Empty<SalesOrder>(),
                SalesOrgSource: identity.Source,
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

        var email = await SafeGetEmailAsync(emailTask, requestingSapUser, ct);

        var response = new MyProfileResponse(
            SapUser: requestingSapUser,
            Role: identity.Role,
            SalesOrg: identity.SalesOrg,
            Email: email,
            Counts: counts,
            Approximate: approximate,
            TopOrders: top,
            SalesOrgSource: identity.Source,
            LoadError: null);

        _logger.LogInformation(
            "MyProfile for {SapUser}: total {Total}, open {Open}, topReturned {Top}, source {Source}, email {HasEmail}",
            requestingSapUser, counts.Total, counts.Open, top.Count, identity.Source,
            !string.IsNullOrWhiteSpace(email));

        return FunctionResult.Ok(response);
    }

    /// <summary>
    /// Awaits the email task and swallows any DB error so a missing link row
    /// never blocks the profile response. The card simply omits the email line.
    /// </summary>
    private async Task<string?> SafeGetEmailAsync(Task<string?> emailTask, string sapUserId, CancellationToken ct)
    {
        try
        {
            var value = await emailTask.WaitAsync(ct);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Email lookup failed for {SapUser}; card will omit email", sapUserId);
            return null;
        }
    }

    /// <summary>
    /// Reads role + sales org from SAP first; falls back to Postgres
    /// (<see cref="IUserScopeLookup"/>) when SAP returns no row, the
    /// <c>UserRoles</c> entity set is not published yet (404), or the call
    /// fails. The returned <see cref="MyProfileSalesOrgSource"/> tells the
    /// card which path contributed the value (useful for debugging and
    /// for future card copy like "from SAP master data").
    /// </summary>
    private async Task<(UserRole Role, string? SalesOrg, MyProfileSalesOrgSource Source)> ResolveIdentityAsync(
        string sapUserId, CancellationToken ct)
    {
        SapUserRoleRow? sapRow = null;
        try
        {
            sapRow = await _sap.GetUserRoleAsync(sapUserId, ct);
        }
        catch (Exception ex)
        {
            // Network / OData failure — never block the profile response.
            _logger.LogWarning(ex,
                "SAP GetUserRoleAsync failed for {SapUser}; falling back to Postgres",
                sapUserId);
        }

        if (sapRow is not null
            && (!string.IsNullOrWhiteSpace(sapRow.Role) || !string.IsNullOrWhiteSpace(sapRow.SalesOrg)))
        {
            // SAP contributes both role + sales org in a single row. We still
            // ask Postgres for the role when SAP returns no role text, because
            // the Postgres mapping is the most up-to-date RBAC source today.
            var role = ParseSapRole(sapRow.Role);
            var pgRole = await _scopeLookup.GetRoleBySapUserAsync(sapUserId, ct);
            var finalRole = role == UserRole.Employee && pgRole > UserRole.Employee
                ? pgRole
                : (role != UserRole.Employee ? role : pgRole);

            // Sales org always comes from SAP when present (single source of truth).
            var salesOrg = string.IsNullOrWhiteSpace(sapRow.SalesOrg)
                ? await _scopeLookup.GetSalesOrgBySapUserAsync(sapUserId, ct)
                : sapRow.SalesOrg;

            return (finalRole, salesOrg, MyProfileSalesOrgSource.SapUserRole);
        }

        // Fallback: Postgres user_mappings only.
        var fallbackRole = await _scopeLookup.GetRoleBySapUserAsync(sapUserId, ct);
        var fallbackOrg = await _scopeLookup.GetSalesOrgBySapUserAsync(sapUserId, ct);
        return (fallbackRole, fallbackOrg, MyProfileSalesOrgSource.Postgres);
    }

    /// <summary>
    /// Maps the SAP-side <c>Role</c> string (e.g. <c>EMPLOYEE</c> /
    /// <c>MANAGER</c> / <c>ADMIN</c>) to <see cref="UserRole"/>. Unknown
    /// values fall back to <see cref="UserRole.Employee"/> so the bot
    /// never blocks the user.
    /// </summary>
    private static UserRole ParseSapRole(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return UserRole.Employee;

        var normalized = raw.Trim().ToUpperInvariant();
        return normalized switch
        {
            "ADMIN" => UserRole.Admin,
            "MANAGER" or "MGR" => UserRole.Manager,
            "EMPLOYEE" or "USER" => UserRole.Employee,
            _ => UserRole.Employee,
        };
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
    string? Email,
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
