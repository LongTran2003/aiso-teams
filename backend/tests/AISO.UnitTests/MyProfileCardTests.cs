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
            Counts: MyProfileOrderCounts.Empty,
            Approximate: false,
            TopOrders: Array.Empty<SalesOrder>(),
            SalesOrgSource: MyProfileSalesOrgSource.Postgres,
            LoadError: "SAP returned 500. Please try again.");

        var json = JsonConvert.SerializeObject(TeamsCardBuilder.BuildMyProfileCard(response).Content);

        Assert.Contains("SAP returned 500", json);
    }
}
