using AISO.Bot.Cards.Builders;
using AISO.SapIntegration.Mock;
using Newtonsoft.Json;
using Xunit;

namespace AISO.UnitTests;

public class SsoDialogValidationTests
{
    [Fact]
    public void BuildLinkSapAccountCard_ShowsRealSapExamples_NotLongTnq()
    {
        var attachment = TeamsCardBuilder.BuildLinkSapAccountCard("Tran Long");
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.Contains("DEV-249", json);
        Assert.Contains("DEV-024", json);
        Assert.DoesNotContain("LONGTNQ", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Link SAP account", json);
    }

    [Fact]
    public void BuildWelcomeCard_MentionsSapUserIdNotDisplayName()
    {
        var attachment = TeamsCardBuilder.BuildWelcomeCard("Tran Long");
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.Contains("SAP User ID", json);
        Assert.Contains("DEV-249", json);
        Assert.DoesNotContain("LONGTNQ", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("DEV-249", true)]
    [InlineData("dev-024", true)]
    [InlineData("LONGTNQ", false)]
    [InlineData("not a user", false)]
    [InlineData("someone@fpt.edu.vn", false)]
    public async Task MockSapUserExists_AcceptsDemoIds(string sapUserId, bool expected)
    {
        var sap = new MockSapClient();
        var exists = await sap.SapUserExistsAsync(sapUserId);
        Assert.Equal(expected, exists);
    }
}
