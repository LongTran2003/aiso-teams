namespace AISO.Domain.Approvals;

/// <summary>One step in the release-approval journey shown on SO detail.</summary>
public sealed record ApprovalJourneyStep(string Title, string Detail);

/// <summary>Builds a simple Requested → Decision (+ Released) trail for Teams cards.</summary>
public static class ApprovalJourney
{
    public static IReadOnlyList<ApprovalJourneyStep> Build(
        OrderApprovalRequest? approval,
        bool orderLooksReleased = false)
    {
        if (approval is null)
            return Array.Empty<ApprovalJourneyStep>();

        var steps = new List<ApprovalJourneyStep>
        {
            new(
                "1. Release requested",
                $"{approval.RequestedBySapUser} · {FormatUtc(approval.RequestedAt)}")
        };

        if (approval.Status == ApprovalStatus.Pending)
        {
            steps.Add(new(
                "2. Manager approval",
                "Waiting — a Manager in this sales org must approve"));
            return steps;
        }

        if (approval.Status == ApprovalStatus.Approved)
        {
            var by = string.IsNullOrWhiteSpace(approval.DecidedBySapUser)
                ? "Manager"
                : approval.DecidedBySapUser!;
            var when = approval.DecidedAt.HasValue
                ? FormatUtc(approval.DecidedAt.Value)
                : "n/a";
            steps.Add(new("2. Approved", $"{by} · {when}"));

            if (orderLooksReleased)
            {
                steps.Add(new(
                    "3. Released in SAP",
                    "Đơn đã duyệt — chờ vận chuyển"));
            }
            else
            {
                steps.Add(new(
                    "3. Approved — check SAP",
                    "Approval recorded; delivery block may still show in SAP."));
            }

            return steps;
        }

        if (approval.Status == ApprovalStatus.Rejected)
        {
            var by = string.IsNullOrWhiteSpace(approval.DecidedBySapUser)
                ? "Manager"
                : approval.DecidedBySapUser!;
            var when = approval.DecidedAt.HasValue
                ? FormatUtc(approval.DecidedAt.Value)
                : "n/a";
            steps.Add(new("2. Approval rejected", $"{by} · {when}"));
            return steps;
        }

        return steps;
    }

    private static string FormatUtc(DateTimeOffset value) =>
        value.ToString("dd MMM yyyy HH:mm") + " UTC";
}
