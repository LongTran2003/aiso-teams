using System.Text.Json;
using AISO.AiOrchestration.Functions;
using AISO.Domain.Approvals;
using AISO.Domain.Kpi;
using AISO.Domain.SalesOrders;
using AISO.Domain.Users;
using AISO.SapIntegration;
using AISO.SapIntegration.Mock;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AISO.UnitTests;

/// <summary>
/// B1: Switch MyProfileFunction to read role + sales org from SAP first,
/// fall back to Postgres (IUserScopeLookup) when SAP has no row or fails.
/// </summary>
public class MyProfileIdentitySourceTests
{
    [Fact]
    public async Task MyProfile_PrefersSapRoleAndSalesOrg_WhenSapReturnsRow()
    {
        var sap = new MockSapClient(); // DEV-249 → (EMPLOYEE, TV01)
        var scope = new StubScopeLookup { Role = UserRole.Manager, SalesOrg = "UE00" };
        var fn = new MyProfileFunction(sap, scope, NullLogger<MyProfileFunction>.Instance);
        using var doc = JsonDocument.Parse("{}");

        var result = await fn.ExecuteAsync(doc.RootElement, "DEV-249");

        Assert.True(result.Success);
        var payload = Assert.IsType<MyProfileResponse>(result.Payload);
        Assert.Equal("TV01", payload.SalesOrg);                       // SAP wins
        Assert.Equal(MyProfileSalesOrgSource.SapUserRole, payload.SalesOrgSource);
    }

    [Fact]
    public async Task MyProfile_AppliesSapRole_WhenSapSaysAdmin()
    {
        var sap = new MockSapClient(); // DEV-230 → (ADMIN, DN00)
        var scope = new StubScopeLookup { Role = UserRole.Employee, SalesOrg = "TV01" };
        var fn = new MyProfileFunction(sap, scope, NullLogger<MyProfileFunction>.Instance);
        using var doc = JsonDocument.Parse("{}");

        var result = await fn.ExecuteAsync(doc.RootElement, "DEV-230");

        Assert.True(result.Success);
        var payload = (MyProfileResponse)result.Payload!;
        Assert.Equal(UserRole.Admin, payload.Role);                 // SAP ADMIN wins
        Assert.Equal("DN00", payload.SalesOrg);
        Assert.Equal(MyProfileSalesOrgSource.SapUserRole, payload.SalesOrgSource);
    }

    [Fact]
    public async Task MyProfile_FallsBackToPostgres_WhenSapHasNoRow()
    {
        // A SAP stub that returns null (unknown user) — like a real SAP would
        // when the user is not in ZAISO_USER_ROLE.
        var sap = new SapUserRoleOverrideClient(_ => Task.FromResult<SapUserRoleRow?>(null));
        var scope = new StubScopeLookup { Role = UserRole.Manager, SalesOrg = "UE00" };
        var fn = new MyProfileFunction(sap, scope, NullLogger<MyProfileFunction>.Instance);
        using var doc = JsonDocument.Parse("{}");

        var result = await fn.ExecuteAsync(doc.RootElement, "DEV-NEW");

        Assert.True(result.Success);
        var payload = (MyProfileResponse)result.Payload!;
        Assert.Equal(UserRole.Manager, payload.Role);               // Postgres
        Assert.Equal("UE00", payload.SalesOrg);
        Assert.Equal(MyProfileSalesOrgSource.Postgres, payload.SalesOrgSource);
    }

    [Fact]
    public async Task MyProfile_FallsBackToPostgres_WhenSapThrows()
    {
        // SAP network / HTTP error: must not break the profile response.
        var sap = new SapUserRoleOverrideClient(
            _ => Task.FromException<SapUserRoleRow?>(new HttpRequestException("SAP unreachable")));
        var scope = new StubScopeLookup { Role = UserRole.Employee, SalesOrg = "TV01" };
        var fn = new MyProfileFunction(sap, scope, NullLogger<MyProfileFunction>.Instance);
        using var doc = JsonDocument.Parse("{}");

        var result = await fn.ExecuteAsync(doc.RootElement, "DEV-NEW");

        Assert.True(result.Success);
        var payload = (MyProfileResponse)result.Payload!;
        Assert.Equal(UserRole.Employee, payload.Role);
        Assert.Equal("TV01", payload.SalesOrg);
        Assert.Equal(MyProfileSalesOrgSource.Postgres, payload.SalesOrgSource);
    }

    [Fact]
    public async Task MyProfile_SapFillingSalesOrgOnly_StillUsesPostgresForRole()
    {
        // When SAP returns a row but Role is blank (CDS quirk), Postgres
        // fills the role and SAP keeps sales org.
        var sap = new SapUserRoleOverrideClient(_ => Task.FromResult<SapUserRoleRow?>(
            new SapUserRoleRow(SapUser: "DEV-249", Role: null, SalesOrg: "DN00")));
        var scope = new StubScopeLookup { Role = UserRole.Manager, SalesOrg = "TV01" };
        var fn = new MyProfileFunction(sap, scope, NullLogger<MyProfileFunction>.Instance);
        using var doc = JsonDocument.Parse("{}");

        var result = await fn.ExecuteAsync(doc.RootElement, "DEV-249");

        Assert.True(result.Success);
        var payload = (MyProfileResponse)result.Payload!;
        Assert.Equal(UserRole.Manager, payload.Role);               // Postgres
        Assert.Equal("DN00", payload.SalesOrg);                     // SAP
        Assert.Equal(MyProfileSalesOrgSource.SapUserRole, payload.SalesOrgSource);
    }

    // --- Test doubles ------------------------------------------------------

    /// <summary>
    /// Wraps <see cref="MockSapClient"/> so test cases can override just
    /// <see cref="GetUserRoleAsync"/> without subclassing the sealed mock.
    /// All other ISapClient methods delegate to the inner mock.
    /// </summary>
    private sealed class SapUserRoleOverrideClient : ISapClient
    {
        private readonly MockSapClient _inner = new();
        private readonly Func<string, Task<SapUserRoleRow?>> _userRole;

        public SapUserRoleOverrideClient(Func<string, Task<SapUserRoleRow?>> userRole)
        {
            _userRole = userRole;
        }

        public Task<SapUserRoleRow?> GetUserRoleAsync(string sapUserId, CancellationToken ct = default)
        {
            try
            {
                return _userRole(sapUserId);
            }
            catch (Exception ex)
            {
                return Task.FromException<SapUserRoleRow?>(ex);
            }
        }

        // --- Delegating members ------------------------------------------
        public Task<IReadOnlyList<SalesOrder>> GetSalesOrdersAsync(SalesOrdersQuery q, CancellationToken ct = default)
            => _inner.GetSalesOrdersAsync(q, ct);
        public Task<SalesOrder?> GetSalesOrderByIdAsync(string so, CancellationToken ct = default)
            => _inner.GetSalesOrderByIdAsync(so, ct);
        public Task<SalesOrder> CreateSalesOrderAsync(CreateSalesOrderDto d, CancellationToken ct = default)
            => _inner.CreateSalesOrderAsync(d, ct);
        public Task SyncUserRoleAsync(string t, string r, string? s, string a, CancellationToken ct = default)
            => _inner.SyncUserRoleAsync(t, r, s, a, ct);
        public Task<SalesOrder> UpdateReferenceAsync(string s, string n, string u, CancellationToken ct = default)
            => _inner.UpdateReferenceAsync(s, n, u, ct);
        public Task<SalesOrder> UpdateSalesOrderAsync(UpdateSalesOrderDto d, CancellationToken ct = default)
            => _inner.UpdateSalesOrderAsync(d, ct);
        public Task<SalesOrder> RejectOrderAsync(string s, string c, string u, CancellationToken ct = default)
            => _inner.RejectOrderAsync(s, c, u, ct);
        public Task<SalesOrder> CancelOrderAsync(string s, string u, string? r = null, CancellationToken ct = default)
            => _inner.CancelOrderAsync(s, u, r, ct);
        public Task DelegateApprovalAsync(DelegateApprovalDto d, CancellationToken ct = default)
            => _inner.DelegateApprovalAsync(d, ct);
        public Task RevokeDelegationAsync(RevokeDelegationDto d, CancellationToken ct = default)
            => _inner.RevokeDelegationAsync(d, ct);
        public Task<SalesOrder> ReleaseOrderAsync(string s, string u, CancellationToken ct = default)
            => _inner.ReleaseOrderAsync(s, u, ct);
        public Task<SalesOrder> ApproveOrderAsync(string s, string u, CancellationToken ct = default)
            => _inner.ApproveOrderAsync(s, u, ct);
        public Task<SalesOrder> RejectApprovalAsync(string s, string u, CancellationToken ct = default)
            => _inner.RejectApprovalAsync(s, u, ct);
        public Task<SalesOrder> ForceReleaseAsync(string s, string u, string r, CancellationToken ct = default)
            => _inner.ForceReleaseAsync(s, u, r, ct);
        public Task<SalesOrder> ForceCancelAsync(string s, string u, string r, CancellationToken ct = default)
            => _inner.ForceCancelAsync(s, u, r, ct);
        public Task<SalesOrder> ReassignOwnerAsync(string s, string n, string u, CancellationToken ct = default)
            => _inner.ReassignOwnerAsync(s, n, u, ct);
        public Task<SalesOrder> ForwardOrderAsync(string s, string f, string u, CancellationToken ct = default, string? r = null)
            => _inner.ForwardOrderAsync(s, f, u, ct, r);
        public Task<KpiSummary> GetKpiSummaryAsync(KpiSummaryQuery q, CancellationToken ct = default)
            => _inner.GetKpiSummaryAsync(q, ct);
        public Task<IReadOnlyList<KpiByCustomer>> GetKpiByCustomerAsync(KpiByCustomerQuery q, CancellationToken ct = default)
            => _inner.GetKpiByCustomerAsync(q, ct);
        public Task<IReadOnlyList<KpiByProduct>> GetKpiByProductAsync(KpiByProductQuery q, CancellationToken ct = default)
            => _inner.GetKpiByProductAsync(q, ct);
        public Task<IReadOnlyList<OverdueOrder>> GetOverdueOrdersAsync(OverdueOrdersQuery q, CancellationToken ct = default)
            => _inner.GetOverdueOrdersAsync(q, ct);
        public Task<bool?> SapUserExistsAsync(string s, CancellationToken ct = default)
            => _inner.SapUserExistsAsync(s, ct);
        public Task<IReadOnlyList<SapSalesArea>> GetSalesAreasAsync(string? s = null, CancellationToken ct = default)
            => _inner.GetSalesAreasAsync(s, ct);
        public Task<IReadOnlyList<SapMaterial>> GetMaterialsAsync(CancellationToken ct = default)
            => _inner.GetMaterialsAsync(ct);
        public Task<IReadOnlyList<SapValidMaterialPlant>> GetValidMaterialPlantsAsync(CancellationToken ct = default)
            => _inner.GetValidMaterialPlantsAsync(ct);
        public Task<IReadOnlyList<SapValidMaterialSales>> GetValidMaterialSalesAsync(string? s = null, string? d = null, int t = 30, CancellationToken ct = default)
            => _inner.GetValidMaterialSalesAsync(s, d, t, ct);
        public Task<IReadOnlyList<SapValidCustomer>> GetValidCustomersAsync(string? s = null, string? d = null, string? div = null, int t = 30, CancellationToken ct = default)
            => _inner.GetValidCustomersAsync(s, d, div, t, ct);
        public Task<bool?> IsCustomerValidForSalesAreaAsync(string c, string s, string d, string div, CancellationToken ct = default)
            => _inner.IsCustomerValidForSalesAreaAsync(c, s, d, div, ct);
        public Task<IReadOnlyList<SapSalesOrg>> GetSalesOrgListAsync(CancellationToken ct = default)
            => _inner.GetSalesOrgListAsync(ct);
        public Task<IReadOnlyList<SapDistChannel>> GetDistChannelListAsync(string? salesOrg = null, CancellationToken ct = default)
            => _inner.GetDistChannelListAsync(salesOrg, ct);
        public Task<IReadOnlyList<SapDivision>> GetDivisionListAsync(string? salesOrg = null, string? distChannel = null, CancellationToken ct = default)
            => _inner.GetDivisionListAsync(salesOrg, distChannel, ct);
        public Task<IReadOnlyList<SapDocType>> GetDocTypeListAsync(CancellationToken ct = default)
            => _inner.GetDocTypeListAsync(ct);
    }

    /// <summary>Static config (no Postgres) for direct MyProfileFunction wiring.</summary>
    private sealed class StubScopeLookup : IUserScopeLookup
    {
        public UserRole Role { get; set; } = UserRole.Employee;
        public string? SalesOrg { get; set; }

        public Task<UserRole> GetRoleBySapUserAsync(string sapUserId, CancellationToken ct = default)
            => Task.FromResult(Role);

        public Task<string?> GetSalesOrgBySapUserAsync(string sapUserId, CancellationToken ct = default)
            => Task.FromResult(SalesOrg);

        public Task<string?> GetDelegatedBySapUserAsync(string sapUserId, CancellationToken ct = default)
            => Task.FromResult<string?>(null);

        public Task<string?> GetEmailBySapUserAsync(string sapUserId, CancellationToken ct = default)
            => Task.FromResult<string?>(null);

        public Task<DelegationInfo> GetDelegationInfoAsync(string sapUserId, CancellationToken ct = default)
            => Task.FromResult(new DelegationInfo(null, null));

        public Task SetDelegatedBySapUserAsync(string delegateUser, string? delegatorUser, DateTimeOffset? validTo = null, decimal? maxAmount = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ActiveDelegation>> GetActiveDelegationsAsync(string? filterDelegatorUser = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ActiveDelegation>>(Array.Empty<ActiveDelegation>());
    }
}
