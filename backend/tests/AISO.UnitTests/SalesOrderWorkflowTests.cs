using AISO.Domain.SalesOrders;
using Xunit;

namespace AISO.UnitTests;

public class SalesOrderWorkflowTests
{
    [Theory]
    [InlineData(SalesOrderStatus.PartiallyDelivered)]
    [InlineData(SalesOrderStatus.Delivered)]
    [InlineData(SalesOrderStatus.Cancelled)]
    public void BlocksReleaseRejectForward_WhenDeliveryStartedOrCancelled(SalesOrderStatus status)
    {
        Assert.True(SalesOrderWorkflow.BlocksReleaseRejectForward(status));
    }

    [Fact]
    public void BuildBlockedMessage_WhenCancelled_UsesRejectedCopy()
    {
        var message = SalesOrderWorkflow.BuildBlockedMessage(SalesOrderStatus.Cancelled, "Reject");
        Assert.Contains("Cancelled", message);
        Assert.Contains("rejected order", message);
    }

    [Theory]
    [InlineData(SalesOrderStatus.Open)]
    [InlineData(SalesOrderStatus.Blocked)]
    [InlineData(SalesOrderStatus.Invoiced)]
    public void DoesNotBlock_WhenStillMutable(SalesOrderStatus status)
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

    [Theory]
    [InlineData(null, "DEV-100", true)]
    [InlineData("", "DEV-100", true)]
    [InlineData("DEV-100", "DEV-100", true)]
    [InlineData("DEV-200", "DEV-100", false)]
    [InlineData("DEV-200", null, false)]
    public void IsCurrentOwner_MatchesSapEmptyOwnerSemantics(string? owner, string? current, bool expected)
    {
        Assert.Equal(expected, SalesOrderWorkflow.IsCurrentOwner(owner, current));
    }
}
