using AISO.Bot.Cards.Builders;
using AISO.Domain.SalesOrders;
using Newtonsoft.Json;
using Xunit;

namespace AISO.UnitTests;

public class SalesOrderRejectionReasonsTests
{
    [Fact]
    public void Catalog_HasShortTitlesAndKnownSapCodes()
    {
        Assert.NotEmpty(SalesOrderRejectionReasons.All);
        Assert.All(SalesOrderRejectionReasons.All, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Code));
            Assert.InRange(r.Title.Length, 1, 24);
            Assert.Contains(r.SapAbgru, new[] { "02", "03", "04" });
        });
    }

    [Theory]
    [InlineData("PRICE_ISSUE", "02")]
    [InlineData("OUT_OF_STOCK", "04")]
    [InlineData("CUSTOMER_CANCEL", "03")]
    [InlineData("OTHER", "03")]
    [InlineData("02", "02")]
    [InlineData("unknown-reason", "03")]
    public void ToSapAbgru_MapsFriendlyOrFallsBack(string input, string expected)
    {
        Assert.Equal(expected, SalesOrderRejectionReasons.ToSapAbgru(input));
    }

    [Fact]
    public void BuildConfirmRejectCard_UsesCatalogChoices()
    {
        var attachment = TeamsCardBuilder.BuildConfirmRejectCard("0000000009");
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.Contains("Price too high", json);
        Assert.Contains("Out of stock", json);
        Assert.Contains("Customer cancelled", json);
        Assert.Contains("Wrong item", json);
        Assert.Contains("Delivery date issue", json);
        Assert.Contains("Credit / payment", json);
        Assert.Contains("Duplicate order", json);
        Assert.Contains("\"value\":\"OTHER\"", json);
        Assert.DoesNotContain("Other / Customer Cancellation", json);
    }
}
