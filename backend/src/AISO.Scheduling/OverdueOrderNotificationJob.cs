using AISO.Domain.Kpi;
using AISO.Domain.Notifications;
using AISO.Persistence;
using AISO.SapIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AISO.Scheduling;

public class OverdueOrderNotificationJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OverdueOrderNotificationJob> _logger;

    public OverdueOrderNotificationJob(IServiceProvider serviceProvider, ILogger<OverdueOrderNotificationJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OverdueOrderNotificationJob started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.Now;

            // Run every day at 8:00 AM (for demo purposes, we will check if it's 8:00 AM)
            // Note: Since this is a simple demo background service, we just sleep until the next 8:00 AM.
            var nextRun = now.Date.AddHours(8);
            if (now > nextRun)
            {
                nextRun = nextRun.AddDays(1);
            }

            var delay = nextRun - now;
            _logger.LogInformation("Next OverdueOrderNotificationJob scheduled in {Delay}", delay);

            await Task.Delay(delay, stoppingToken);

            if (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOverdueOrdersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing OverdueOrderNotificationJob.");
                }
            }
        }
    }

    public async Task ProcessOverdueOrdersAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var sapClient = scope.ServiceProvider.GetRequiredService<ISapClient>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // Get all managers from SapLinkAssignment (assuming it's where authorized users are)
        var managers = await db.SapLinkAssignments
            .Where(a => a.Role == AISO.Domain.Users.UserRole.Manager && a.TeamsEmail != null && a.SalesOrg != null)
            .ToListAsync(ct);

        if (managers.Count == 0)
        {
            _logger.LogInformation("No managers found. Skipping overdue notification.");
            return;
        }

        foreach (var manager in managers)
        {
            if (string.IsNullOrEmpty(manager.TeamsEmail) || string.IsNullOrEmpty(manager.SalesOrg))
                continue;

            _logger.LogInformation("Fetching overdue orders for Manager {Email} (SalesOrg: {SalesOrg})", manager.TeamsEmail, manager.SalesOrg);

            var query = new OverdueOrdersQuery
            {
                SalesOrg = manager.SalesOrg,
                DaysPastDue = 1,
                Top = 50
            };

            try
            {
                var overdueOrders = await sapClient.GetOverdueOrdersAsync(query, ct);

                if (overdueOrders.Count > 0)
                {
                    await SendOverdueEmailAsync(emailService, manager.TeamsEmail, manager.SalesOrg, overdueOrders, ct);
                }
                else
                {
                    _logger.LogInformation("No overdue orders found for Manager {Email}", manager.TeamsEmail);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process overdue orders for Manager {Email}", manager.TeamsEmail);
            }
        }
    }

    private async Task SendOverdueEmailAsync(IEmailService emailService, string email, string salesOrg, IReadOnlyList<OverdueOrder> orders, CancellationToken ct)
    {
        string subject = $"[Alert] Overdue Sales Orders Report - {salesOrg} ({DateTime.Now:dd/MM/yyyy})";

        var rows = string.Join("\n", orders.Select(o => $@"
            <tr>
                <td style='border: 1px solid #ddd; padding: 8px;'>{o.SoNumber}</td>
                <td style='border: 1px solid #ddd; padding: 8px;'>{o.CustomerName}</td>
                <td style='border: 1px solid #ddd; padding: 8px;'>{o.NetValue} {o.Currency}</td>
                <td style='border: 1px solid #ddd; padding: 8px;'>{o.ScheduledDeliveryDate:dd/MM/yyyy}</td>
                <td style='border: 1px solid #ddd; padding: 8px; color: red;'><b>{o.DaysPastDue} day(s)</b></td>
            </tr>
        "));

        string html = $@"
            <div style='font-family: Arial, sans-serif;'>
                <h2 style='color: #d9534f;'>Overdue Sales Orders Alert - Area {salesOrg}</h2>
                <p>Dear Manager,</p>
                <p>The system has identified <b>{orders.Count}</b> sales order(s) that have exceeded their scheduled delivery date. Please review and take timely action.</p>

                <table style='border-collapse: collapse; width: 100%; margin-top: 20px;'>
                    <thead>
                        <tr style='background-color: #f2f2f2;'>
                            <th style='border: 1px solid #ddd; padding: 8px; text-align: left;'>Sales Order</th>
                            <th style='border: 1px solid #ddd; padding: 8px; text-align: left;'>Customer</th>
                            <th style='border: 1px solid #ddd; padding: 8px; text-align: left;'>Net Value</th>
                            <th style='border: 1px solid #ddd; padding: 8px; text-align: left;'>Scheduled Delivery</th>
                            <th style='border: 1px solid #ddd; padding: 8px; text-align: left;'>Days Overdue</th>
                        </tr>
                    </thead>
                    <tbody>
                        {rows}
                    </tbody>
                </table>
                <br>
                <p>Best regards,<br>AISO Teams Bot</p>
            </div>
        ";

        await emailService.SendEmailAsync(email, subject, html, ct);
    }
}
