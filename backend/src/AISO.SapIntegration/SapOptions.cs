namespace AISO.SapIntegration;

public class SapOptions
{
    public const string SectionName = "Sap";

    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
}
