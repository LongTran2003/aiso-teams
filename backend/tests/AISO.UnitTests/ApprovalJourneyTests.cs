using AISO.Domain.Approvals;
using Xunit;

namespace AISO.UnitTests;

public class ApprovalJourneyTests
{
    [Fact]
    public void Build_WhenNull_ReturnsEmpty()
    {
        Assert.Empty(ApprovalJourney.Build(null));
    }

    [Fact]
    public void Build_WhenPending_ShowsRequestedAndWaiting()
    {
        var requestedAt = new DateTimeOffset(2026, 8, 4, 3, 0, 0, TimeSpan.Zero);
        var steps = ApprovalJourney.Build(new OrderApprovalRequest
        {
            Id = Guid.NewGuid(),
            SoNumber = "0000000001",
            RequestedBySapUser = "DEV-024",
            Status = ApprovalStatus.Pending,
            RequestedAt = requestedAt
        });

        Assert.Equal(2, steps.Count);
        Assert.Contains("DEV-024", steps[0].Detail);
        Assert.Contains("Waiting", steps[1].Detail);
    }

    [Fact]
    public void Build_WhenApproved_ShowsApproverAndOptionalReleased()
    {
        var steps = ApprovalJourney.Build(
            new OrderApprovalRequest
            {
                Id = Guid.NewGuid(),
                SoNumber = "0000000001",
                RequestedBySapUser = "DEV-024",
                Status = ApprovalStatus.Approved,
                DecidedBySapUser = "DEV-249",
                RequestedAt = new DateTimeOffset(2026, 8, 4, 3, 0, 0, TimeSpan.Zero),
                DecidedAt = new DateTimeOffset(2026, 8, 4, 4, 0, 0, TimeSpan.Zero)
            },
            orderLooksReleased: true);

        Assert.Equal(3, steps.Count);
        Assert.Contains("DEV-249", steps[1].Detail);
        Assert.Contains("Approved", steps[1].Title);
        Assert.Contains("Released in SAP", steps[2].Title);
        Assert.Contains("chờ vận chuyển", steps[2].Detail);
    }

    [Fact]
    public void Build_WhenApprovedButStillBlocked_ShowsCheckSapStep()
    {
        var steps = ApprovalJourney.Build(
            new OrderApprovalRequest
            {
                Id = Guid.NewGuid(),
                SoNumber = "0000000001",
                RequestedBySapUser = "DEV-024",
                Status = ApprovalStatus.Approved,
                DecidedBySapUser = "DEV-249",
                RequestedAt = new DateTimeOffset(2026, 8, 4, 3, 0, 0, TimeSpan.Zero),
                DecidedAt = new DateTimeOffset(2026, 8, 4, 4, 0, 0, TimeSpan.Zero)
            },
            orderLooksReleased: false);

        Assert.Equal(3, steps.Count);
        Assert.Contains("check SAP", steps[2].Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_WhenRejected_ShowsRejector()
    {
        var steps = ApprovalJourney.Build(new OrderApprovalRequest
        {
            Id = Guid.NewGuid(),
            SoNumber = "0000000001",
            RequestedBySapUser = "DEV-024",
            Status = ApprovalStatus.Rejected,
            DecidedBySapUser = "DEV-249",
            RequestedAt = new DateTimeOffset(2026, 8, 4, 3, 0, 0, TimeSpan.Zero),
            DecidedAt = new DateTimeOffset(2026, 8, 4, 4, 30, 0, TimeSpan.Zero)
        });

        Assert.Equal(2, steps.Count);
        Assert.Contains("rejected", steps[1].Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DEV-249", steps[1].Detail);
    }
}
