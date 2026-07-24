using AISO.Bot.Cards.Builders;
using AISO.Domain.Approvals;
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
        Assert.Contains("filter_pending_approvals", json);
        Assert.Contains("Input.ChoiceSet", json);
    }

    [Fact]
    public void BuildPendingApprovalsCard_FiltersAndKeepsDynamicChoices()
    {
        var requestedAt = new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
        var approvals = new[]
        {
            Approval("0000000009", "DEV-024", "UE00", "priority", requestedAt),
            Approval("0000000010", "DEV-025", "US00", "standard", requestedAt)
        };

        var attachment = TeamsCardBuilder.BuildPendingApprovalsCard(
            approvals,
            search: "priority",
            requester: "DEV-024",
            salesOrg: "UE00");

        var json = JsonConvert.SerializeObject(attachment.Content);
        Assert.Contains("0000000009", json);
        Assert.DoesNotContain("0000000010", json);
        Assert.Contains("DEV-025", json);
        Assert.Contains("US00", json);
        Assert.Contains("\"value\":\"priority\"", json);
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

    private static OrderApprovalRequest Approval(
        string soNumber,
        string requestedBy,
        string salesOrg,
        string comment,
        DateTimeOffset requestedAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            SoNumber = soNumber,
            RequestedBySapUser = requestedBy,
            SalesOrg = salesOrg,
            Comment = comment,
            Status = ApprovalStatus.Pending,
            RequestedAt = requestedAt
        };
}
