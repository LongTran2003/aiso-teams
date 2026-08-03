using AISO.Bot.Cards.Builders;
using Newtonsoft.Json;
using Xunit;

namespace AISO.UnitTests;

public class HelpCardTests
{
    [Theory]
    [InlineData("Employee", "Employee flow", "Request release", "recent orders")]
    [InlineData("Manager", "Manager flow", "Pending approvals", "approve order")]
    [InlineData("Admin", "Admin flow", "List users", "manage user")]
    public void BuildHelpCard_ShowsRoleFlowAndShortcuts_NotFunctionSchemas(
        string role,
        string flowTitle,
        string shortcut,
        string sample)
    {
        var attachment = TeamsCardBuilder.BuildHelpCard(role);
        var json = JsonConvert.SerializeObject(attachment.Content);

        Assert.Contains("How to use AISO", json);
        Assert.Contains(flowTitle, json);
        Assert.Contains(shortcut, json);
        Assert.Contains(sample, json);
        Assert.DoesNotContain("CheckOrderStatus", json);
        Assert.DoesNotContain("<function=", json);
        Assert.DoesNotContain("SUPPORTED COMMANDS", json);
    }
}
