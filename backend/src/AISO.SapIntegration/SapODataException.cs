namespace AISO.SapIntegration;

/// <summary>
/// Represents a structured error from SAP OData.
/// Carries a parsed, human-readable message instead of raw JSON.
/// </summary>
public sealed class SapODataException : Exception
{
    public int HttpStatusCode { get; }

    public SapODataException(int httpStatusCode, string message)
        : base(message)
    {
        HttpStatusCode = httpStatusCode;
    }

    /// <summary>
    /// True when the SAP error message indicates a business validation failure
    /// (bad material, customer, plant, etc.) rather than a system/infra error.
    /// </summary>
    public bool IsValidationError => IsValidationMessage(Message);

    public static bool IsValidationMessage(string? message) =>
        message is not null
        && (message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
            || message.Contains("not maintained", StringComparison.OrdinalIgnoreCase)
            || message.Contains("not extended to plant", StringComparison.OrdinalIgnoreCase)
            || message.Contains("not valid for", StringComparison.OrdinalIgnoreCase)
            || message.Contains("is not found", StringComparison.OrdinalIgnoreCase)
            || message.Contains("invalid material", StringComparison.OrdinalIgnoreCase));
}
