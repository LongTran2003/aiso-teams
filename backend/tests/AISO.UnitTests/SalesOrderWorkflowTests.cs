using AISO.Domain.SalesOrders;
using Xunit;

namespace AISO.UnitTests;

public class SalesOrderWorkflowTests
{
    [Theory]
    [InlineData(SalesOrderStatus.PartiallyDelivered)]
    [InlineData(SalesOrderStatus.Delivered)]
    public void BlocksReleaseRejectForward_WhenDeliveryStarted(SalesOrderStatus status)
    {
        Assert.True(SalesOrderWorkflow.BlocksReleaseRejectForward(status));
        Assert.Contains("not allowed after delivery", SalesOrderWorkflow.BuildBlockedMessage(status, "Reject"));
    }

    [Theory]
    [InlineData(SalesOrderStatus.Open)]
    [InlineData(SalesOrderStatus.Blocked)]
    [InlineData(SalesOrderStatus.Invoiced)]
    [InlineData(SalesOrderStatus.Cancelled)]
    public void DoesNotBlock_WhenNotDelivered(SalesOrderStatus status)
    {
        Assert.False(SalesOrderWorkflow.BlocksReleaseRejectForward(status));
    }

    [Fact]
    public void BuildPendingApprovalBlockedMessage_IncludesRequesterAndAction()
    {
        var message = SalesOrderWorkflow.BuildPendingApprovalBlockedMessage("Reject", "DEV-100");

        Assert.Contains("pending release request", message);
        Assert.Contains("DEV-100", message);
        Assert.Contains("Reject", message);
        Assert.Contains("approves or rejects", message);
    }
}
