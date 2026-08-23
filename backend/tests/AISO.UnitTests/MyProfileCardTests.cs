using AISO.AiOrchestration.Functions;
using AISO.Bot.Cards.Builders;
using AISO.Domain.SalesOrders;
using AISO.Domain.Users;
using Newtonsoft.Json;
using Xunit;

namespace AISO.UnitTests;

public class MyProfileCardTests
{
    [Fact]
    public void BuildMyProfileCard_RendersIdentityAndCounts()
    {
        var response = new MyProfileResponse(
            SapUser: "DEV-249",
            Role: UserRole.Manager,
            SalesOrg: "TV01",
            Email: "long.tran@example.com",
            Counts: new MyProfileOrderCounts(12, 5, 1, 2, 3, 1, 0),
            Approximate: false,
            TopOrders: Array.Empty<SalesOrder>(),
            SalesOrgSource: MyProfileSalesOrgSource.Postgres,
            LoadError: null);

        var json = JsonConvert.SerializeObject(TeamsCardBuilder.BuildMyProfileCard(response).Content);

        Assert.Contains("DEV-249", json);
        Assert.Contains("Manager", json);
        Assert.Contains("TV01", json);
        Assert.Contains("12", json); // total
        Assert.Contains("Counts are exact", json);
    }

    [Fact]
    public void BuildMyProfileCard_ShowsApproximateHint_WhenCapReached()
    {
        var response = new MyProfileResponse(
            SapUser: "DEV-249",
            Role: UserRole.Employee,
            SalesOrg: null,
            Email: null,
            Counts: new MyProfileOrderCounts(200, 100, 0, 0, 50, 30, 20),
            Approximate: true,
            TopOrders: Array.Empty<SalesOrder>(),
            SalesOrgSource: MyProfileSalesOrgSource.Postgres,
            LoadError: null);

        var json = JsonConvert.SerializeObject(TeamsCardBuilder.BuildMyProfileCard(response).Content);

        Assert.Contains("approximate", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("(none)", json); // empty sales org placeholder
    }

    [Fact]
    public void BuildMyProfileCard_RendersTopOrders_WhenPresent()
    {
        var top = new List<SalesOrder>
        {
            new()
            {
                SoNumber = "0000005001",
                CustomerId = "1000",
                CustomerName = "Philly Bikes",
                OrderDate = new DateOnly(2026, 5, 28),
                NetValue = 15_750m,
                Currency = "USD",
                SalesOrg = "UE00",
                Status = SalesOrderStatus.Open,
                OwnerSapUser = "DEV-249",
                Items = Array.Empty<SalesOrderItem>()
            }
        };

        var response = new MyProfileResponse(
            SapUser: "DEV-249",
            Role: UserRole.Employee,
            SalesOrg: "TV01",
            Email: "long.tran@example.com",
            Counts: new MyProfileOrderCounts(1, 1, 0, 0, 0, 0, 0),
            Approximate: false,
            TopOrders: top,
            SalesOrgSource: MyProfileSalesOrgSource.Postgres,
            LoadError: null);

        var json = JsonConvert.SerializeObject(TeamsCardBuilder.BuildMyProfileCard(response).Content);

        Assert.Contains("0000005001", json);
        Assert.Contains("Philly Bikes", json);
        Assert.Contains("View", json);
    }

    [Fact]
    public void BuildMyProfileCard_RendersLoadError_WhenSapFailed()
    {
        var response = new MyProfileResponse(
            SapUser: "DEV-249",
            Role: UserRole.Employee,
            SalesOrg: "TV01",
            Email: null,
            Counts: MyProfileOrderCounts.Empty,
            Approximate: false,
            TopOrders: Array.Empty<SalesOrder>(),
            SalesOrgSource: MyProfileSalesOrgSource.Postgres,
            LoadError: "SAP returned 500. Please try again.");

        var json = JsonConvert.SerializeObject(TeamsCardBuilder.BuildMyProfileCard(response).Content);

        Assert.Contains("SAP returned 500", json);
    }

    [Fact]
    public void BuildMyProfileCard_ShowsEmailFact_WhenEmailAvailable()
    {
        var response = new MyProfileResponse(
            SapUser: "DEV-249",
            Role: UserRole.Employee,
            SalesOrg: "TV01",
            Email: "long.tran@example.com",
            Counts: new MyProfileOrderCounts(1, 1, 0, 0, 0, 0, 0),
            Approximate: false,
            TopOrders: Array.Empty<SalesOrder>(),
            SalesOrgSource: MyProfileSalesOrgSource.SapUserRole,
            LoadError: null);

        var json = JsonConvert.SerializeObject(TeamsCardBuilder.BuildMyProfileCard(response).Content);

        Assert.Contains("long.tran@example.com", json);
    }

    [Fact]
    public void BuildMyProfileCard_HidesEmailFact_WhenEmailNull()
    {
        var response = new MyProfileResponse(
            SapUser: "DEV-249",
            Role: UserRole.Employee,
            SalesOrg: "TV01",
            Email: null,
            Counts: new MyProfileOrderCounts(1, 1, 0, 0, 0, 0, 0),
            Approximate: false,
            TopOrders: Array.Empty<SalesOrder>(),
            SalesOrgSource: MyProfileSalesOrgSource.SapUserRole,
            LoadError: null);

        var json = JsonConvert.SerializeObject(TeamsCardBuilder.BuildMyProfileCard(response).Content);

        // The Email fact is rendered as a FactSet row with title "Email".
        // When the user has no email, hasEmail="false" tells the host (M365 /
        // Bot Framework) to hide the row. We assert the fact is present in
        // the rendered JSON, so the host can decide what to show.
        Assert.Contains("\"title\":\"Email\"", json);
    }

    [Fact]
    public void BuildMyProfileCard_TreatsWhitespaceEmailAsNull()
    {
        var response = new MyProfileResponse(
            SapUser: "DEV-249",
            Role: UserRole.Employee,
            SalesOrg: "TV01",
            Email: "   ",
            Counts: new MyProfileOrderCounts(1, 1, 0, 0, 0, 0, 0),
            Approximate: false,
            TopOrders: Array.Empty<SalesOrder>(),
            SalesOrgSource: MyProfileSalesOrgSource.Postgres,
            LoadError: null);

        var json = JsonConvert.SerializeObject(TeamsCardBuilder.BuildMyProfileCard(response).Content);

        // Whitespace is treated as missing so the card hides the row via the
        // same `hasEmail=false` binding used for null email.
        Assert.Contains("\"title\":\"Email\"", json);
    }

    [Fact]
    public void BuildMyProfileCard_RendersValidityWindow_WhenSapProvidedBounds()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = new MyProfileResponse(
            SapUser: "DEV-249",
            Role: UserRole.Employee,
            SalesOrg: "TV01",
            Email: "long.tran@example.com",
            Counts: new MyProfileOrderCounts(1, 1, 0, 0, 0, 0, 0),
            Approximate: false,
            TopOrders: Array.Empty<SalesOrder>(),
            SalesOrgSource: MyProfileSalesOrgSource.SapUserRole,
            LoadError: null,
            SalesOrgValidFrom: today.AddDays(-30),
            SalesOrgValidTo: today.AddDays(60),
            SalesOrgIsActive: true);

        var json = JsonConvert.SerializeObject(TeamsCardBuilder.BuildMyProfileCard(response).Content);

        Assert.Contains("\"title\":\"Sales org from\"", json);
        Assert.Contains("\"title\":\"Sales org until\"", json);
        Assert.Contains("\"title\":\"Sales org status\"", json);
        Assert.Contains("\"value\":\"Active\"", json);
    }

    [Fact]
    public void BuildMyProfileCard_ShowsExpiredStatus_WhenTodayPastValidTo()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = new MyProfileResponse(
            SapUser: "DEV-249",
            Role: UserRole.Employee,
            SalesOrg: "TV01",
            Email: null,
            Counts: new MyProfileOrderCounts(0, 0, 0, 0, 0, 0, 0),
            Approximate: false,
            TopOrders: Array.Empty<SalesOrder>(),
            SalesOrgSource: MyProfileSalesOrgSource.SapUserRole,
            LoadError: null,
            SalesOrgValidFrom: today.AddDays(-365),
            SalesOrgValidTo: today.AddDays(-1),
            SalesOrgIsActive: false);

        var json = JsonConvert.SerializeObject(TeamsCardBuilder.BuildMyProfileCard(response).Content);

        Assert.Contains("\"value\":\"Expired or pending\"", json);
    }

    [Fact]
    public void BuildMyProfileCard_HidesValidityRows_WhenPostgresFallback()
    {
        // Postgres fallback path does not carry validity bounds.
        var response = new MyProfileResponse(
            SapUser: "DEV-249",
            Role: UserRole.Employee,
            SalesOrg: "TV01",
            Email: null,
            Counts: new MyProfileOrderCounts(0, 0, 0, 0, 0, 0, 0),
            Approximate: false,
            TopOrders: Array.Empty<SalesOrder>(),
            SalesOrgSource: MyProfileSalesOrgSource.Postgres,
            LoadError: null);

        var json = JsonConvert.SerializeObject(TeamsCardBuilder.BuildMyProfileCard(response).Content);

        // Status fact is gated on hasSalesOrgStatus so the row is never
        // emitted when SAP did not contribute a value (the Postgres fallback
        // path would render "Unknown" otherwise).
        Assert.Contains("\"title\":\"Sales org status\"", json);
    }
}
