namespace AISO.SapIntegration;

public class SapOptions
{
    public const string SectionName = "Sap";

    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// When true, the bot uses <see cref="MockSapClient"/> instead of the real SAP OData/RFC client.
    /// Useful when the SAP test tenant has missing/invalid master data (e.g. material/plant extensions).
    /// Configured via environment variable <c>AISO__Sap__UseMock=true</c> or appsettings <c>Sap:UseMock</c>.
    /// </summary>
    public bool UseMock { get; set; }
}
