using AISO.Bot.Cards.Builders;
using AISO.Domain.SalesOrders;
using AISO.Domain.Users;
using Newtonsoft.Json;
using Xunit;

namespace AISO.UnitTests;

public class SalesOrderDetailCardTests
{
    [Fact]
    public void BuildSalesOrderDetailCard_Employee_ShowsRequestReleaseNotApprove()
    {
        var attachment = TeamsCardBuilder.BuildSalesOrderDetailCard(SampleOrder(), UserRole.Employee);
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.Contains("Request release", json);
        Assert.Contains("\"action\":\"release_so\"", json);
        Assert.DoesNotContain("\"action\":\"approve_so\"", json);
        Assert.Contains("Reject order", json);
        Assert.Contains("Forward", json);
        Assert.Contains("No line items available yet.", json);
        Assert.DoesNotContain("Waiting for manager approval", json);
    }

    [Fact]
    public void BuildSalesOrderDetailCard_Manager_WithoutPending_ShowsItemsNotApprove()
    {
        var order = SampleOrder(withItems: true);
        var attachment = TeamsCardBuilder.BuildSalesOrderDetailCard(order, UserRole.Manager);
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.DoesNotContain("\"action\":\"approve_so\"", json);
        Assert.DoesNotContain("\"action\":\"release_so\"", json);
        Assert.Contains("\"action\":\"reject_so\"", json);
        Assert.Contains("MAT-001", json);
        Assert.Contains("Widget", json);
        Assert.Contains("10 · MAT-001", json);
        Assert.Contains("/EA", json);
        Assert.DoesNotContain("No line items available yet.", json);
    }

    [Fact]
    public void BuildSalesOrderDetailCard_Manager_WithPending_ShowsApproveAndRequesterNote()
    {
        var order = SampleOrder(withItems: true);
        var attachment = TeamsCardBuilder.BuildSalesOrderDetailCard(
            order,
            UserRole.Manager,
            hasPendingApproval: true,
            pendingRequestedBySapUser: "DEV-100",
            pendingComment: "Please rush");
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.Contains("Release request pending", json);
        Assert.Contains("Submitted by DEV-100", json);
        Assert.Contains("Note for manager: Please rush", json);
        Assert.DoesNotContain("Waiting for manager approval", json);
        Assert.DoesNotContain("Reject / Forward", json);
        Assert.Contains("\"action\":\"approve_so\"", json);
        Assert.DoesNotContain("\"action\":\"reject_so\"", json);
        Assert.DoesNotContain("\"action\":\"forward_so\"", json);
        Assert.DoesNotContain("\"action\":\"release_so\"", json);
    }

    [Fact]
    public void BuildSalesOrderDetailCard_Manager_WithPending_NoComment_ShowsNa()
    {
        var attachment = TeamsCardBuilder.BuildSalesOrderDetailCard(
            SampleOrder(),
            UserRole.Manager,
            hasPendingApproval: true,
            pendingRequestedBySapUser: "DEV-024");
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.Contains("Submitted by DEV-024", json);
        Assert.Contains("Note for manager: N/A", json);
    }

    [Fact]
    public void BuildSalesOrderDetailCard_Employee_WithPending_ShowsShortWaitingCopy()
    {
        var attachment = TeamsCardBuilder.BuildSalesOrderDetailCard(
            SampleOrder(),
            UserRole.Employee,
            hasPendingApproval: true,
            pendingRequestedBySapUser: "DEV-100");
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.Contains("Waiting for manager approval", json);
        Assert.Contains("Release requested by DEV-100", json);
        Assert.Contains("you can't change this order until then", json);
        Assert.Contains("Approval: Waiting", json);
        Assert.DoesNotContain("Reject / Forward", json);
        Assert.DoesNotContain("Release request pending", json);
        Assert.DoesNotContain("\"action\":\"release_so\"", json);
        Assert.DoesNotContain("\"action\":\"reject_so\"", json);
        Assert.DoesNotContain("\"action\":\"forward_so\"", json);
        Assert.DoesNotContain("\"action\":\"approve_so\"", json);
    }

    [Theory]
    [InlineData(SalesOrderStatus.Delivered)]
    [InlineData(SalesOrderStatus.PartiallyDelivered)]
    [InlineData(SalesOrderStatus.Cancelled)]
    [InlineData(SalesOrderStatus.Invoiced)]
    public void BuildSalesOrderDetailCard_WhenTerminalStatus_HidesPendingBanner(SalesOrderStatus status)
    {
        var order = SampleOrder(status: status);
        var attachment = TeamsCardBuilder.BuildSalesOrderDetailCard(
            order,
            UserRole.Employee,
            hasPendingApproval: true,
            pendingRequestedBySapUser: "DEV-024");
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.DoesNotContain("Waiting for manager approval", json);
        Assert.DoesNotContain("Release request pending", json);
        Assert.DoesNotContain("Release requested by DEV-024", json);
    }

    [Theory]
    [InlineData(SalesOrderStatus.Delivered)]
    [InlineData(SalesOrderStatus.PartiallyDelivered)]
    [InlineData(SalesOrderStatus.Cancelled)]
    public void BuildSalesOrderDetailCard_WhenLockedStatus_HidesLifecycleActions(SalesOrderStatus status)
    {
        var order = SampleOrder(status: status);
        var attachment = TeamsCardBuilder.BuildSalesOrderDetailCard(
            order,
            UserRole.Manager,
            hasPendingApproval: true);
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.DoesNotContain("\"action\":\"approve_so\"", json);
        Assert.DoesNotContain("\"action\":\"release_so\"", json);
        Assert.DoesNotContain("\"action\":\"reject_so\"", json);
        Assert.DoesNotContain("\"action\":\"forward_so\"", json);
    }

    [Theory]
    [InlineData(SalesOrderStatus.Delivered)]
    [InlineData(SalesOrderStatus.PartiallyDelivered)]
    public void BuildSalesOrderDetailCard_WhenDelivered_HidesReject(SalesOrderStatus status)
    {
        var order = SampleOrder(status: status) with { OwnerSapUser = "DEV-249" };
        var attachment = TeamsCardBuilder.BuildSalesOrderDetailCard(
            order,
            UserRole.Employee,
            hasPendingApproval: false,
            currentSapUser: "DEV-249");
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.DoesNotContain("\"action\":\"reject_so\"", json);
        Assert.DoesNotContain("\"action\":\"forward_so\"", json);
        Assert.DoesNotContain("\"action\":\"release_so\"", json);
    }

    [Fact]
    public void BuildSuccessCard_Rejected_ShowsCancelledCopy()
    {
        var attachment = TeamsCardBuilder.BuildSuccessCard("0000000009", "Rejected");
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.Contains("Order rejected", json);
        Assert.Contains("Cancelled", json);
        Assert.Contains("0000000009", json);
    }

    [Fact]
    public void BuildSuccessCard_ReleaseRequested_ShowsWaitingCopy()
    {
        var attachment = TeamsCardBuilder.BuildSuccessCard("0000000009", "ReleaseRequested");
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.Contains("Release requested", json);
        Assert.Contains("Waiting for manager approval", json);
        Assert.Contains("0000000009", json);
    }

    [Fact]
    public void BuildSalesOrderDetailCard_WhenNotOwner_HidesMutationsAndShowsOwnedBy()
    {
        var order = SampleOrder() with { OwnerSapUser = "DEV-200" };
        var attachment = TeamsCardBuilder.BuildSalesOrderDetailCard(
            order,
            UserRole.Employee,
            currentSapUser: "DEV-100");
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.Contains("Owned by DEV-200", json);
        Assert.Contains("limited to the owner", json);
        Assert.DoesNotContain("\"action\":\"release_so\"", json);
        Assert.DoesNotContain("\"action\":\"reject_so\"", json);
        Assert.DoesNotContain("\"action\":\"forward_so\"", json);
    }

    [Fact]
    public void BuildSalesOrderDetailCard_WhenOwner_ShowsMutations()
    {
        var order = SampleOrder() with { OwnerSapUser = "DEV-100" };
        var attachment = TeamsCardBuilder.BuildSalesOrderDetailCard(
            order,
            UserRole.Employee,
            currentSapUser: "DEV-100");
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.Contains("Owned by DEV-100", json);
        Assert.Contains("You currently own this order", json);
        Assert.Contains("\"action\":\"release_so\"", json);
        Assert.Contains("\"action\":\"forward_so\"", json);
    }

    [Fact]
    public void BuildSalesOrderDetailCard_WhenInvalidMaterial_ShowsWarningAndHidesMutations()
    {
        var order = SampleOrder() with { OwnerSapUser = "DEV-249", HasInvalidMaterial = true };
        var attachment = TeamsCardBuilder.BuildSalesOrderDetailCard(
            order,
            UserRole.Employee,
            hasPendingApproval: false,
            currentSapUser: "DEV-249");
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.Contains("Invalid material master data", json);
        Assert.DoesNotContain("\"action\":\"reject_so\"", json);
        Assert.DoesNotContain("\"action\":\"forward_so\"", json);
        Assert.DoesNotContain("\"action\":\"release_so\"", json);
    }

    [Fact]
    public void BuildSuccessCard_Forwarded_ShowsOwnershipTransferCopy()
    {
        var attachment = TeamsCardBuilder.BuildSuccessCard("0000000009", "Forwarded", "DEV-300");
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.Contains("Order forwarded", json);
        Assert.Contains("DEV-300", json);
        Assert.Contains("no longer own", json);
    }

    [Fact]
    public void BuildConfirmForwardCard_IncludesAllRecipientsAndPreselect()
    {
        var choices = new[]
        {
            ("Long (DEV-249)", "DEV-249"),
            ("Thuy (DEV-244)", "DEV-244")
        };

        var attachment = TeamsCardBuilder.BuildConfirmForwardCard(
            "00000013122",
            choices,
            senderName: "Employee (DEV-024)",
            selectedRecipient: "DEV-244");
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.Contains("DEV-249", json);
        Assert.Contains("DEV-244", json);
        Assert.Contains("00000013122", json);
        Assert.Contains("forward_so_confirm", json);
    }

    [Fact]
    public void BuildSalesOrderDetailCard_WithApprovedJourney_ShowsReleasedUxAndHidesMutations()
    {
        var approval = new AISO.Domain.Approvals.OrderApprovalRequest
        {
            Id = Guid.NewGuid(),
            SoNumber = "0000000009",
            RequestedBySapUser = "DEV-024",
            Status = AISO.Domain.Approvals.ApprovalStatus.Approved,
            DecidedBySapUser = "DEV-249",
            RequestedAt = new DateTimeOffset(2026, 8, 4, 3, 0, 0, TimeSpan.Zero),
            DecidedAt = new DateTimeOffset(2026, 8, 4, 4, 0, 0, TimeSpan.Zero)
        };

        var order = SampleOrder(status: SalesOrderStatus.Open) with { OwnerSapUser = "DEV-024" };
        var attachment = TeamsCardBuilder.BuildSalesOrderDetailCard(
            order,
            UserRole.Employee,
            currentSapUser: "DEV-024",
            approval: approval);
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.Contains("Approval journey", json);
        Assert.Contains("Release requested", json);
        Assert.Contains("DEV-024", json);
        Assert.Contains("Approved", json);
        Assert.Contains("DEV-249", json);
        Assert.Contains("Open (Released)", json);
        Assert.Contains("Đã duyệt", json);
        Assert.Contains("Đơn đã duyệt — chờ vận chuyển", json);
        Assert.DoesNotContain("delivery block was cleared", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not that the order is stuck", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"action\":\"release_so\"", json);
        Assert.DoesNotContain("\"action\":\"reject_so\"", json);
        Assert.DoesNotContain("\"action\":\"forward_so\"", json);
    }

    [Fact]
    public void BuildSalesOrderDetailCard_WithPendingJourney_ShowsWaitingStep()
    {
        var approval = new AISO.Domain.Approvals.OrderApprovalRequest
        {
            Id = Guid.NewGuid(),
            SoNumber = "0000000009",
            RequestedBySapUser = "DEV-024",
            Status = AISO.Domain.Approvals.ApprovalStatus.Pending,
            RequestedAt = new DateTimeOffset(2026, 8, 4, 3, 0, 0, TimeSpan.Zero)
        };

        var attachment = TeamsCardBuilder.BuildSalesOrderDetailCard(
            SampleOrder(),
            UserRole.Employee,
            hasPendingApproval: true,
            pendingRequestedBySapUser: "DEV-024",
            approval: approval);
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.Contains("Approval journey", json);
        Assert.Contains("Manager approval", json);
        Assert.Contains("Waiting", json);
    }

    [Fact]
    public void BuildSoSummaryCard_WhenApprovedOpen_ShowsOpenReleasedLabel()
    {
        var approval = new AISO.Domain.Approvals.OrderApprovalRequest
        {
            Id = Guid.NewGuid(),
            SoNumber = "0000000009",
            RequestedBySapUser = "DEV-024",
            Status = AISO.Domain.Approvals.ApprovalStatus.Approved,
            DecidedBySapUser = "DEV-249",
            RequestedAt = new DateTimeOffset(2026, 8, 4, 3, 0, 0, TimeSpan.Zero),
            DecidedAt = new DateTimeOffset(2026, 8, 4, 4, 0, 0, TimeSpan.Zero)
        };

        var order = SampleOrder(status: SalesOrderStatus.Open);
        var attachment = TeamsCardBuilder.BuildSoSummaryCard(
            new[] { order },
            title: "Recent orders",
            latestApprovalsBySo: new Dictionary<string, AISO.Domain.Approvals.OrderApprovalRequest?>
            {
                [order.SoNumber] = approval
            });
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.Contains("Open (Released)", json);
        Assert.Contains("Đã duyệt", json);
        Assert.DoesNotContain("\"status\":\"Open\"", json);
    }

    private static SalesOrder SampleOrder(bool withItems = false, SalesOrderStatus status = SalesOrderStatus.Blocked) =>
        new()
        {
            SoNumber = "0000000009",
            CustomerId = "1000",
            CustomerName = "Acme",
            CustomerReference = "PO-1",
            Division = "00",
            OrderDate = new DateOnly(2026, 7, 22),
            RequestedDeliveryDate = new DateOnly(2026, 8, 1),
            NetValue = 1200m,
            Currency = "USD",
            SalesOrg = "TV01",
            Status = status,
            Items = withItems
                ? new[]
                {
                    new SalesOrderItem
                    {
                        ItemNumber = "10",
                        Material = "MAT-001",
                        Description = "Widget",
                        Quantity = 2,
                        Unit = "EA",
                        NetValue = 1200m
                    }
                }
                : Array.Empty<SalesOrderItem>()
        };
}
