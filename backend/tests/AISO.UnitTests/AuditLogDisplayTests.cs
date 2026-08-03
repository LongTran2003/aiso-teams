using AISO.Domain.Auditing;
using Xunit;

namespace AISO.UnitTests;

public class AuditLogDisplayTests
{
    [Theory]
    [InlineData("ListBotUsers", "List users")]
    [InlineData("ManageBotUser", "Manage user")]
    [InlineData("ai_text_reply", "AI reply")]
    [InlineData("ViewAuditLog", "View audit log")]
    public void FriendlyAction_MapsKnownFunctions(string raw, string expected)
    {
        Assert.Equal(expected, AuditLogDisplay.FriendlyAction(raw));
    }

    [Fact]
    public void FormatDuration_SubSecond_UsesMilliseconds()
    {
        Assert.Equal("658 ms", AuditLogDisplay.FormatDuration(658));
    }

    [Fact]
    public void FormatDuration_SecondsMinutesHours()
    {
        Assert.Equal("18s", AuditLogDisplay.FormatDuration(18_899));
        Assert.Equal("2m 5s", AuditLogDisplay.FormatDuration(125_000));
        Assert.Equal("1h 2m 3s", AuditLogDisplay.FormatDuration(3_723_000));
    }

    [Fact]
    public void FormatDuration_Null_IsNa()
    {
        Assert.Equal("n/a", AuditLogDisplay.FormatDuration(null));
    }

    [Fact]
    public void FormatUserLabel_PrefersNameAndSapId()
    {
        Assert.Equal(
            "Long Tran (DEV-249)",
            AuditLogDisplay.FormatUserLabel("Long Tran", "DEV-249", "29:abc"));
    }
}
