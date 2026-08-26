using AISO.Domain.Users;

namespace AISO.Bot.Notifications;

/// <summary>
/// Builds the HTML body and the change-detection predicate for the
/// "your AISO access has been updated" email sent when an admin changes a
/// user's role and/or sales org via the manage_bot_user_confirm flow.
/// </summary>
public static class UserAccessChangeEmailBuilder
{
    /// <summary>
    /// Returns true when the new role or sales-org differs from the old values.
    /// Comparison is case-insensitive and treats null / whitespace as equivalent.
    /// </summary>
    public static bool HasChange(UserRole oldRole, UserRole newRole, string? oldSalesOrg, string? newSalesOrg)
    {
        if (oldRole != newRole)
        {
            return true;
        }

        var normalizedOld = string.IsNullOrWhiteSpace(oldSalesOrg) ? null : oldSalesOrg.Trim();
        var normalizedNew = string.IsNullOrWhiteSpace(newSalesOrg) ? null : newSalesOrg.Trim();

        if (normalizedOld is null && normalizedNew is null)
        {
            return false;
        }

        if (normalizedOld is null || normalizedNew is null)
        {
            return true;
        }

        return !string.Equals(normalizedOld, normalizedNew, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds the HTML body. Display name and admin SAP user are HTML-encoded to
    /// keep the rendered email safe even if the caller forgets to sanitize.
    /// </summary>
    public static string Build(
        string displayName,
        string adminSapUser,
        UserRole oldRole,
        UserRole newRole,
        string? oldSalesOrg,
        string? newSalesOrg)
    {
        return $@"
            <h2>Your AISO Teams Bot access was updated</h2>
            <p>Hi {HtmlEncode(displayName)},</p>
            <p>An administrator (<b>{HtmlEncode(adminSapUser)}</b>) updated your AISO Teams Bot access.</p>
            <ul>
                <li><b>Previous role:</b> {HtmlEncode(oldRole.ToString())}</li>
                <li><b>New role:</b> {HtmlEncode(newRole.ToString())}</li>
                <li><b>Previous sales org:</b> {HtmlEncode(string.IsNullOrWhiteSpace(oldSalesOrg) ? "(none)" : oldSalesOrg)}</li>
                <li><b>New sales org:</b> {HtmlEncode(string.IsNullOrWhiteSpace(newSalesOrg) ? "(none)" : newSalesOrg)}</li>
            </ul>
            <p>The change has been synced to SAP. If you have pending approval lists open, you may need to sign out and back in to refresh them.</p>
            <p style=""color:#888;font-size:12px"">This is an automated message from the AISO Teams Bot. Please do not reply.</p>
        ";
    }

    private static string HtmlEncode(string value) => System.Net.WebUtility.HtmlEncode(value);
}
