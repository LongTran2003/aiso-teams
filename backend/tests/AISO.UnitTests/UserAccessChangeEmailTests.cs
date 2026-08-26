using AISO.Bot.Notifications;
using AISO.Domain.Users;
using Xunit;

namespace AISO.UnitTests;

/// <summary>
/// Unit tests for the user-access-change email builder used by the
/// <c>manage_bot_user_confirm</c> admin flow.
/// </summary>
public class UserAccessChangeEmailTests
{
    [Fact]
    public void BuildsEmail_WithDisplayNameAndAdmin()
    {
        var html = UserAccessChangeEmailBuilder.Build(
            displayName: "Alice Nguyen",
            adminSapUser: "DEV-001",
            oldRole: UserRole.Employee,
            newRole: UserRole.Manager,
            oldSalesOrg: "TV01",
            newSalesOrg: "FU24");

        Assert.Contains("Alice Nguyen", html);
        Assert.Contains("DEV-001", html);
        Assert.Contains("Employee", html);
        Assert.Contains("Manager", html);
        Assert.Contains("TV01", html);
        Assert.Contains("FU24", html);
    }

    [Fact]
    public void ReplacesMissingSalesOrgs_WithNonePlaceholder()
    {
        var html = UserAccessChangeEmailBuilder.Build(
            displayName: "Bob",
            adminSapUser: "DEV-002",
            oldRole: UserRole.Admin,
            newRole: UserRole.Manager,
            oldSalesOrg: null,
            newSalesOrg: "DS00");

        Assert.Contains("(none)", html);
        Assert.Contains("DS00", html);
    }

    [Fact]
    public void HtmlEncodesDisplayName_ToPreventInjection()
    {
        var html = UserAccessChangeEmailBuilder.Build(
            displayName: "<script>alert(1)</script>",
            adminSapUser: "DEV-003",
            oldRole: UserRole.Employee,
            newRole: UserRole.Employee,
            oldSalesOrg: "TV01",
            newSalesOrg: "TV01");

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void DetectsChange_WhenOnlyRoleChanges()
    {
        Assert.True(UserAccessChangeEmailBuilder.HasChange(
            oldRole: UserRole.Employee,
            newRole: UserRole.Manager,
            oldSalesOrg: "TV01",
            newSalesOrg: "TV01"));
    }

    [Fact]
    public void DetectsChange_WhenOnlySalesOrgChanges()
    {
        Assert.True(UserAccessChangeEmailBuilder.HasChange(
            oldRole: UserRole.Employee,
            newRole: UserRole.Employee,
            oldSalesOrg: "TV01",
            newSalesOrg: "FU24"));
    }

    [Fact]
    public void NoChange_WhenBothUnchanged()
    {
        Assert.False(UserAccessChangeEmailBuilder.HasChange(
            oldRole: UserRole.Manager,
            newRole: UserRole.Manager,
            oldSalesOrg: "TV01",
            newSalesOrg: "TV01"));
    }

    [Fact]
    public void DetectsChange_FromNullToValue_AndBack()
    {
        Assert.True(UserAccessChangeEmailBuilder.HasChange(
            oldRole: UserRole.Employee,
            newRole: UserRole.Employee,
            oldSalesOrg: null,
            newSalesOrg: "TV01"));

        Assert.True(UserAccessChangeEmailBuilder.HasChange(
            oldRole: UserRole.Employee,
            newRole: UserRole.Employee,
            oldSalesOrg: "TV01",
            newSalesOrg: null));
    }

    [Fact]
    public void SalesOrgComparison_IsCaseInsensitive()
    {
        Assert.False(UserAccessChangeEmailBuilder.HasChange(
            oldRole: UserRole.Employee,
            newRole: UserRole.Employee,
            oldSalesOrg: "tv01",
            newSalesOrg: "TV01"));
    }
}
