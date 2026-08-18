using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using AISO.Domain.Notifications;

namespace AISO.Reporting.Email;

public class GraphEmailService : IEmailService
{
    private readonly GraphServiceClient? _graphClient;
    private readonly string _senderEmail;
    private readonly ILogger<GraphEmailService> _logger;

    public GraphEmailService(IConfiguration configuration, ILogger<GraphEmailService> logger)
    {
        _logger = logger;

        var tenantId = configuration["MicrosoftAppTenantId"];
        var clientId = configuration["MicrosoftAppId"];
        var clientSecret = configuration["MicrosoftAppPassword"];
        _senderEmail = configuration["Graph:SenderEmail"] ?? throw new InvalidOperationException("Graph:SenderEmail is not configured");

        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            _logger.LogWarning("Missing Azure AD credentials. Emails will not be sent.");
            return;
        }

        var options = new ClientSecretCredentialOptions
        {
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
        };

        var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret, options);
        _graphClient = new GraphServiceClient(clientSecretCredential, new[] { "https://graph.microsoft.com/.default" });
    }

    public async Task SendEmailAsync(string toAddress, string subject, string htmlContent, CancellationToken ct = default)
    {
        if (_graphClient == null)
        {
            _logger.LogWarning("Cannot send email. GraphClient is not initialized due to missing credentials.");
            return;
        }

        var requestBody = new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody
        {
            Message = new Message
            {
                Subject = subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = htmlContent
                },
                ToRecipients = new List<Recipient>
                {
                    new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = toAddress
                        }
                    }
                }
            },
            SaveToSentItems = true
        };

        try
        {
            await _graphClient.Users[_senderEmail]
                .SendMail
                .PostAsync(requestBody, cancellationToken: ct);

            _logger.LogInformation("Successfully sent email to {ToAddress} with subject: {Subject}", toAddress, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToAddress}", toAddress);
        }
    }
}
