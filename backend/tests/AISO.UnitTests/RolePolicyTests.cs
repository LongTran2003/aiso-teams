using AISO.AiOrchestration;
using AISO.Domain.Users;
using Xunit;

namespace AISO.UnitTests;

public class RolePolicyTests
{
    [Theory]
    [InlineData("GetSalesOrders")]
    [InlineData("CheckOrderStatus")]
    [InlineData("GetOrderDetail")]
    [InlineData("GetKpiSummary")]
    [InlineData("GetOverdueOrders")]
    [InlineData("ai_text_reply")]
    public void ReadAndChitchatFunctions_AreAllowedForEveryone(string function)
    {
        Assert.True(RolePolicy.CanExecute(UserRole.Employee, function));
        Assert.True(RolePolicy.CanExecute(UserRole.Manager, function));
        Assert.True(RolePolicy.CanExecute(UserRole.Admin, function));
    }

    [Fact]
    public void UnknownFunction_DefaultsToEmployee()
    {
        Assert.Equal(UserRole.Employee, RolePolicy.RequiredRole("SomeBrandNewFunction"));
        Assert.True(RolePolicy.CanExecute(UserRole.Employee, "SomeBrandNewFunction"));
    }

    [Fact]
    public void Employee_CannotReleaseOrder()
    {
        // Maker-checker: an Employee submits for approval, they do not release directly.
        Assert.False(RolePolicy.CanExecute(UserRole.Employee, "ReleaseOrder"));
    }

    [Theory]
    [InlineData("ReleaseOrder")]
    [InlineData("ReassignOwner")]
    public void Manager_CanRunApprovalActions(string function)
    {
        Assert.Equal(UserRole.Manager, RolePolicy.RequiredRole(function));
        Assert.False(RolePolicy.CanExecute(UserRole.Employee, function));
        Assert.True(RolePolicy.CanExecute(UserRole.Manager, function));
        Assert.True(RolePolicy.CanExecute(UserRole.Admin, function));
    }

    [Theory]
    [InlineData("ApproveOrder")]
    [InlineData("RejectApproval")]
    [InlineData("GetPendingApprovals")]
    [InlineData("ApproveSelectedOrders")]
    public void DelegatedActions_AreAllowedForEmployeeAtPolicyLevel(string function)
    {
        // Allowed at the orchestration routing level so Employee can trigger them;
        // actual delegation checks happen inside the function logic.
        Assert.Equal(UserRole.Employee, RolePolicy.RequiredRole(function));
        Assert.True(RolePolicy.CanExecute(UserRole.Employee, function));
    }

    [Theory]
    [InlineData("ForceRelease")]
    [InlineData("ForceCancel")]
    [InlineData("ViewAuditLog")]
    [InlineData("ListBotUsers")]
    [InlineData("ManageBotUser")]
    public void OverrideActions_AreAdminOnly(string function)
    {
        Assert.Equal(UserRole.Admin, RolePolicy.RequiredRole(function));
        Assert.False(RolePolicy.CanExecute(UserRole.Employee, function));
        Assert.False(RolePolicy.CanExecute(UserRole.Manager, function));
        Assert.True(RolePolicy.CanExecute(UserRole.Admin, function));
    }

    [Fact]
    public void Employee_CanRequestRelease()
    {
        Assert.True(RolePolicy.CanExecute(UserRole.Employee, "RequestRelease"));
    }

    [Theory]
    [InlineData("RejectOrder")]
    [InlineData("ForwardOrder")]
    [InlineData("UpdateOrderReference")]
    [InlineData("CreateOrder")]
    [InlineData("EditOrder")]
    [InlineData("CancelOrder")]
    public void OwnerWriteActions_AreAllowedForEmployee(string function)
    {
        Assert.True(RolePolicy.CanExecute(UserRole.Employee, function));
    }
}
