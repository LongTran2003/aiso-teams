using AISO.Bot.Cards.Builders;
using Newtonsoft.Json;
using Xunit;

namespace AISO.UnitTests;

public class PendingApprovalsCardTests
{
    [Fact]
    public void BuildPendingApprovalsCard_RendersOrderAndRequester()
    {
        var attachment = TeamsCardBuilder.BuildPendingApprovalsCard(new
        {
            count = 1,
            items = new[]
            {
                new
                {
                    orderId = "0000000009",
                    requestedBy = "DEV-024",
                    salesOrg = "UE00",
                    comment = "please approve",
                    requestedAt = "2026-07-22 12:00:00Z"
                }
            }
        });

        var json = JsonConvert.SerializeObject(attachment.Content);
        Assert.Contains("Pending approvals", json);
        Assert.Contains("0000000009", json);
        Assert.Contains("DEV-024", json);
        Assert.Contains("Approve", json);
        Assert.Contains("Reject approval", json);
    }

    [Fact]
    public void BuildNotAuthorizedCard_ShowsRoles()
    {
        var attachment = TeamsCardBuilder.BuildNotAuthorizedCard(
            "You do not have permission.",
            "Employee",
            "Manager");

        var json = JsonConvert.SerializeObject(attachment.Content);
        Assert.Contains("Not authorized", json);
        Assert.Contains("Employee", json);
        Assert.Contains("Manager", json);
    }

    [Fact]
    public void BuildOverdueOrdersCard_RendersDaysLate()
    {
        var attachment = TeamsCardBuilder.BuildOverdueOrdersCard(new
        {
            count = 1,
            orders = new[]
            {
                new
                {
                    soNumber = "0000000001",
                    customerName = "Acme",
                    daysPastDue = 5,
                    formattedValue = "100 USD",
                    scheduledDeliveryDate = "01 Jan 2026",
                    salesOrg = "UE00"
                }
            }
        });

        var json = JsonConvert.SerializeObject(attachment.Content);
        Assert.Contains("Overdue orders", json);
        Assert.Contains("5 days late", json);
        Assert.Contains("0000000001", json);
    }
}
