namespace AISO.Domain.Notifications;

public interface IEmailService
{
    Task SendEmailAsync(string toAddress, string subject, string htmlContent, CancellationToken ct = default);
}
