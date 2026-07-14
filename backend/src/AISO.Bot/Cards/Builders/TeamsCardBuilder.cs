using System.Text.Json;
using AISO.Domain.SalesOrders;
using Microsoft.Bot.Schema;

namespace AISO.Bot.Cards.Builders;

internal static class TeamsCardBuilder
{
    public static Attachment BuildWelcomeCard(string username) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("welcome.json", new { username });

    public static Attachment BuildHelpCard() =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("help.json");

    public static Attachment BuildEmptyCard() =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("empty.json");

    public static Attachment BuildLoadingCard() =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("loading.json");

    public static Attachment BuildSuccessCard(string salesOrderNumber, string status) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("success.json", new { salesOrderNumber, status });

    public static Attachment BuildErrorCard(string errorCode, string errorMessage) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("error.json", new { errorCode, errorMessage });

    public static Attachment BuildConfirmRejectCard(string salesOrderNumber) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("confirm-reject.json", new { salesOrderNumber });

    public static Attachment BuildConfirmReleaseCard(string salesOrderNumber) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("confirm-release.json", new { salesOrderNumber });

    public static Attachment BuildConfirmForwardCard(string salesOrderNumber, IEnumerable<(string Title, string Value)>? choices = null)
    {
        var recipientChoices = (choices ?? Array.Empty<(string Title, string Value)>())
            .Select(choice => new { title = choice.Title, value = choice.Value })
            .ToList();

        return CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "confirm-forward.json",
            new { salesOrderNumber, choices = recipientChoices });
    }

    public static Attachment BuildKpiSummaryCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("kpi-summary.json", data);

    public static Attachment BuildKpiRevenueCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("kpi-revenue.json", data);

    public static Attachment BuildKpiDeliveryCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("kpi-delivery.json", data);

    public static Attachment BuildSalesOrderDetailCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("sales-order-detail.json", data);

    public static Attachment? BuildKpiCardForRequest(string message, IReadOnlyList<SalesOrder> orders, string? chartUrl)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var lowerMessage = message.ToLowerInvariant();
        var currency = orders.FirstOrDefault()?.Currency ?? "USD";
        var totalRevenue = orders.Sum(o => o.NetValue);
        var targetRevenue = totalRevenue + Math.Max(10000m, totalRevenue * 0.1m);

        if (lowerMessage.Contains("delivery"))
        {
            var deliveredCount = orders.Count(o => o.Status is SalesOrderStatus.Delivered or SalesOrderStatus.Invoiced);
            var delayedCount = orders.Count(o => o.Status is SalesOrderStatus.Blocked or SalesOrderStatus.PartiallyDelivered or SalesOrderStatus.Open);
            var onTimeRate = orders.Count == 0 ? 0 : Math.Round((double)deliveredCount / orders.Count * 100, 0);

            return BuildKpiDeliveryCard(new
            {
                onTimeRate = $"{onTimeRate}%",
                delayedCount = delayedCount.ToString(),
                completedToday = deliveredCount.ToString(),
                deliveryProgress = (int)onTimeRate,
                chartUrl
            });
        }

        if (lowerMessage.Contains("revenue"))
        {
            return BuildKpiRevenueCard(new
            {
                period = "Current results",
                totalRevenue = $"{totalRevenue:N0} {currency}",
                growthRate = orders.Count > 5 ? "+12%" : "+8%",
                targetRevenue = $"{targetRevenue:N0} {currency}",
                chartUrl
            });
        }

        if (lowerMessage.Contains("kpi") || lowerMessage.Contains("summary"))
        {
            return BuildKpiSummaryCard(new
            {
                revenueValue = $"{totalRevenue:N0} {currency}",
                orderCount = orders.Count,
                chartUrl
            });
        }

        return null;
    }

    public static bool TryBuildWorkflowSuccessCard(JsonElement payload, string? functionName, out Attachment? card)
    {
        card = null;
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!payload.TryGetProperty("order_id", out var orderIdElement) || orderIdElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(orderIdElement.GetString()))
        {
            return false;
        }

        var action = payload.TryGetProperty("action", out var actionElement) && actionElement.ValueKind == JsonValueKind.String
            ? actionElement.GetString()
            : functionName;

        card = BuildSuccessCard(orderIdElement.GetString()!, action ?? "Completed");
        return true;
    }

    public static Attachment BuildSoSummaryCard(IReadOnlyList<SalesOrder> orders)
    {
        var data = new
        {
            count = orders.Count,
            orders = orders.Select(o => new
            {
                soNumber = o.SoNumber,
                customerName = o.CustomerName,
                orderDate = o.OrderDate.ToString("dd MMM yyyy"),
                formattedValue = $"{o.NetValue:N0} {o.Currency}",
                status = o.Status.ToString(),
                statusColor = StatusToColor(o.Status),
                salesOrg = o.SalesOrg
            }).ToList()
        };

        return CardTemplateFileLoader.BuildAdaptiveCardAttachment("so-summary.json", data);
    }

    private static string StatusToColor(SalesOrderStatus s) => s switch
    {
        SalesOrderStatus.Blocked => "Attention",
        SalesOrderStatus.Open or SalesOrderStatus.PartiallyDelivered => "Warning",
        SalesOrderStatus.Delivered or SalesOrderStatus.Invoiced => "Good",
        _ => "Default"
    };
}
