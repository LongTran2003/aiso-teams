using AISO.AiOrchestration;
using Xunit;

namespace AISO.UnitTests;

public class AiServiceDispatcherShortcutTests
{
    [Theory]
    [InlineData("view audit log")]
    [InlineData("View Audit Log")]
    [InlineData("list users")]
    [InlineData("manage user DEV-249")]
    [InlineData("set role DEV-001")]
    public void IsDeterministicShortcut_MatchesAdminHelpPhrases(string message)
    {
        Assert.True(AiServiceDispatcher.IsDeterministicShortcut(message));
    }

    [Theory]
    [InlineData("show open orders")]
    [InlineData("request release 13122")]
    [InlineData("hello")]
    public void IsDeterministicShortcut_IgnoresNormalCommands(string message)
    {
        Assert.False(AiServiceDispatcher.IsDeterministicShortcut(message));
    }

    [Theory]
    [InlineData("GetAuditLog", "ViewAuditLog")]
    [InlineData("GetAuditLogs", "ViewAuditLog")]
    [InlineData("ViewAuditLog", "ViewAuditLog")]
    public void NormalizeFunctionAlias_MapsAuditMisnomers(string input, string expected)
    {
        Assert.Equal(expected, AiServiceDispatcher.NormalizeFunctionAlias(input));
    }
}
