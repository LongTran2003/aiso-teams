using Microsoft.Extensions.Configuration;

namespace AISO.Domain.Approvals;

public static class ApprovalThresholdHelper
{
    /// <summary>
    /// Returns the threshold for the given currency, or null if no threshold is configured.
    /// Looks up "ApprovalThresholds:ManagerMaxAmount:{currency}" first,
    /// falls back to "ApprovalThresholds:ManagerMaxAmount" (legacy single value).
    /// </summary>
    public static decimal? GetThreshold(IConfiguration config, string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            return null;

        var currencyKey = $"ApprovalThresholds:ManagerMaxAmount:{currency.ToUpperInvariant()}";
        var currencyThreshold = config.GetValue<decimal?>(currencyKey);
        
        if (currencyThreshold.HasValue)
            return currencyThreshold;

        // Fallback to legacy single value
        return config.GetValue<decimal?>("ApprovalThresholds:ManagerMaxAmount");
    }

    /// <summary>
    /// Checks if NetValue exceeds the threshold for the order's currency.
    /// Returns a user-friendly error message if exceeded, or null if OK.
    /// </summary>
    public static string? CheckThreshold(IConfiguration config, decimal netValue, string? currency)
    {
        var threshold = GetThreshold(config, currency);
        
        if (threshold.HasValue && netValue > threshold.Value)
        {
            return $"Order value ({netValue:N2}) exceeds threshold ({threshold.Value:N2} {currency}).";
        }
        
        return null;
    }
}
