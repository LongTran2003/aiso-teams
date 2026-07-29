namespace AISO.Domain.SalesOrders;

/// <summary>
/// Canonical rejection reasons for Teams confirm cards and AI <c>RejectOrder</c>.
/// Titles stay short for the ChoiceSet; values map to SAP ABGRU codes currently
/// allowed by RAP <c>rejectOrder</c> (02 / 03 / 04).
/// </summary>
public static class SalesOrderRejectionReasons
{
    public sealed record Reason(string Code, string Title, string SapAbgru);

    public static IReadOnlyList<Reason> All { get; } =
    [
        new("PRICE_ISSUE", "Price too high", "02"),
        new("OUT_OF_STOCK", "Out of stock", "04"),
        new("CUSTOMER_CANCEL", "Customer cancelled", "03"),
        new("WRONG_ITEM", "Wrong item", "03"),
        new("DELIVERY_DATE", "Delivery date issue", "03"),
        new("CREDIT_ISSUE", "Credit / payment", "03"),
        new("DUPLICATE_ORDER", "Duplicate order", "03"),
        new("OTHER", "Other", "03"),
    ];

    public static bool TryGet(string? codeOrTitle, out Reason reason)
    {
        reason = null!;
        if (string.IsNullOrWhiteSpace(codeOrTitle))
            return false;

        var key = codeOrTitle.Trim();
        var match = All.FirstOrDefault(r =>
            string.Equals(r.Code, key, StringComparison.OrdinalIgnoreCase)
            || string.Equals(r.Title, key, StringComparison.OrdinalIgnoreCase)
            || string.Equals(r.SapAbgru, key, StringComparison.OrdinalIgnoreCase));

        if (match is null)
            return false;

        reason = match;
        return true;
    }

    /// <summary>Maps a friendly / AI reason to SAP ABGRU; unknown values fall back to Other (03).</summary>
    public static string ToSapAbgru(string? codeOrTitle)
    {
        if (TryGet(codeOrTitle, out var reason))
            return reason.SapAbgru;

        return "03";
    }

    public static string ToCanonicalCode(string? codeOrTitle)
    {
        if (TryGet(codeOrTitle, out var reason))
            return reason.Code;

        return "OTHER";
    }

    public static IReadOnlyList<string> Codes { get; } =
        All.Select(r => r.Code).ToList();
}
