namespace AISO.Domain.Auditing;

/// <summary>Human-readable labels for Admin audit Adaptive Card.</summary>
public static class AuditLogDisplay
{
    private static readonly IReadOnlyDictionary<string, string> ActionLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ListBotUsers"] = "List users",
            ["ManageBotUser"] = "Manage user",
            ["ViewAuditLog"] = "View audit log",
            ["GetPendingApprovals"] = "Pending approvals",
            ["RequestRelease"] = "Request release",
            ["ApproveOrder"] = "Approve order",
            ["RejectApproval"] = "Reject approval",
            ["ReleaseOrder"] = "Release order",
            ["RejectOrder"] = "Reject order",
            ["ForwardOrder"] = "Forward order",
            ["ForceRelease"] = "Force release",
            ["ForceCancel"] = "Force cancel",
            ["ReassignOwner"] = "Reassign owner",
            ["GetSalesOrders"] = "List sales orders",
            ["CheckOrderStatus"] = "Check order",
            ["GetOrderDetail"] = "Order detail",
            ["GetKpiSummary"] = "KPI summary",
            ["GetKpiByCustomer"] = "KPI by customer",
            ["GetKpiByProduct"] = "KPI by product",
            ["GetOverdueOrders"] = "Overdue orders",
            ["CreateOrder"] = "Create order",
            ["UpdateOrderReference"] = "Update reference",
            ["ai_text_reply"] = "AI reply",
            ["unrecognized"] = "Unrecognized",
            ["GetAuditLog"] = "View audit log (bad name)",
            ["GetAuditLogs"] = "View audit log (bad name)",
        };

    public static string FriendlyAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return "Unknown";

        return ActionLabels.TryGetValue(action.Trim(), out var label)
            ? label
            : SplitPascalOrSnake(action.Trim());
    }

    /// <summary>
    /// Formats wall-clock duration for the audit card.
    /// Sub-second stays in ms; longer values use h / m / s.
    /// </summary>
    public static string FormatDuration(int? durationMs)
    {
        if (durationMs is null || durationMs < 0)
            return "n/a";

        var ms = durationMs.Value;
        if (ms < 1000)
            return $"{ms} ms";

        var totalSeconds = ms / 1000;
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;

        if (hours > 0)
            return $"{hours}h {minutes}m {seconds}s";
        if (minutes > 0)
            return $"{minutes}m {seconds}s";
        return $"{seconds}s";
    }

    public static string FormatUserLabel(string? displayName, string? sapUserId, string teamsUserId)
    {
        var name = displayName?.Trim();
        var sap = sapUserId?.Trim();

        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(sap))
            return $"{name} ({sap})";
        if (!string.IsNullOrWhiteSpace(name))
            return name!;
        if (!string.IsNullOrWhiteSpace(sap))
            return sap!;

        // Last resort: short Teams id (full id is unusable on card)
        if (string.IsNullOrWhiteSpace(teamsUserId))
            return "Unknown";
        return teamsUserId.Length <= 24
            ? teamsUserId
            : teamsUserId[..12] + "…";
    }

    private static string SplitPascalOrSnake(string raw)
    {
        if (raw.Contains('_'))
        {
            return string.Join(' ', raw.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));
        }

        var chars = new List<char>(raw.Length + 8);
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(raw[i - 1]))
                chars.Add(' ');
            chars.Add(i == 0 ? char.ToUpperInvariant(c) : c);
        }

        return new string(chars.ToArray());
    }
}
