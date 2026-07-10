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
}
