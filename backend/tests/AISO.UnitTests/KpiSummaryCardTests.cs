using AISO.Bot.Cards.Builders;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using Xunit;

namespace AISO.UnitTests;

public class KpiSummaryCardTests
{
    private static string BuildCardJson(object data)
    {
        var attachment = TeamsCardBuilder.BuildKpiSummaryCard(data);
        return JsonConvert.SerializeObject(attachment.Content);
    }

    private static string BuildRevenueCardJson(object data)
    {
        var attachment = TeamsCardBuilder.BuildKpiRevenueCard(data);
        return JsonConvert.SerializeObject(attachment.Content);
    }

    private static string BuildDeliveryCardJson(object data)
    {
        var attachment = TeamsCardBuilder.BuildKpiDeliveryCard(data);
        return JsonConvert.SerializeObject(attachment.Content);
    }

    [Fact]
    public void BuildKpiSummaryCard_RendersRevenueOrdersAndStatusCounts()
    {
        var json = BuildCardJson(new
        {
            period = "2026-01-01 to 2026-07-01",
            revenueValue = "125,000 USD",
            orderCount = 42,
            openOrders = 10,
            deliveredOrders = 30,
            overdueOrders = 2,
            fulfillmentRate = "71.4%",
            chartUrl = "https://quickchart.io/chart?c=abc"
        });

        Assert.Contains("125,000 USD", json);
        Assert.Contains("42", json);
        Assert.Contains("71.4%", json);
        Assert.Contains("Delivered", json);
        Assert.Contains("Overdue", json);
        // Chart image is present when a chart URL is supplied.
        Assert.Contains("https://quickchart.io/chart?c=abc", json);
    }

    [Fact]
    public void BuildKpiSummaryCard_OmitsChartImage_WhenChartUrlEmpty()
    {
        var json = BuildCardJson(new
        {
            period = "All time",
            revenueValue = "0 USD",
            orderCount = 0,
            openOrders = 0,
            deliveredOrders = 0,
            overdueOrders = 0,
            fulfillmentRate = "0%",
            chartUrl = ""
        });

        // The $when guard removes the Image element when there is no chart URL.
        Assert.DoesNotContain("\"Image\"", json);
    }

    [Fact]
    public void BuildKpiRevenueCard_OmitsChartImage_WhenChartUrlEmpty()
    {
        var json = BuildRevenueCardJson(new
        {
            period = "Current results",
            totalRevenue = "0 USD",
            growthRate = "+0%",
            targetRevenue = "0 USD",
            chartUrl = ""
        });

        Assert.DoesNotContain("\"Image\"", json);
    }

    [Fact]
    public void BuildKpiDeliveryCard_OmitsChartImage_WhenChartUrlEmpty()
    {
        var json = BuildDeliveryCardJson(new
        {
            onTimeRate = "0%",
            delayedCount = "0",
            completedToday = "0",
            deliveryProgress = 0,
            chartUrl = ""
        });

        Assert.DoesNotContain("\"Image\"", json);
    }
}
