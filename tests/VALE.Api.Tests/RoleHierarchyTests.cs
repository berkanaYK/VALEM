using VALE.Api.Domain;

namespace VALE.Api.Tests;

public sealed class RoleHierarchyTests
{
    [Fact]
    public void Owner_can_assign_every_supported_role()
    {
        Assert.True(Roles.CanAssignRoles([Roles.Owner], Roles.All));
    }

    [Theory]
    [InlineData(Roles.Admin, Roles.Owner)]
    [InlineData(Roles.OperationsManager, Roles.Admin)]
    [InlineData(Roles.BranchManager, Roles.OperationsManager)]
    [InlineData(Roles.Supervisor, Roles.BranchManager)]
    [InlineData(Roles.Valet, Roles.Supervisor)]
    public void Lower_role_cannot_assign_equal_or_higher_role(string actor, string requested)
    {
        Assert.False(Roles.CanAssignRoles([actor], [requested]));
    }

    [Theory]
    [InlineData(Roles.Admin, Roles.BranchManager)]
    [InlineData(Roles.OperationsManager, Roles.Supervisor)]
    [InlineData(Roles.BranchManager, Roles.Valet)]
    [InlineData(Roles.Supervisor, Roles.Valet)]
    public void Higher_role_can_manage_lower_role(string actor, string target)
    {
        Assert.True(Roles.CanManageTarget([actor], [target]));
    }

    [Fact]
    public void Auditor_is_not_an_operation_or_finance_writer()
    {
        Assert.DoesNotContain(Roles.Auditor, Roles.OperationWriteRoles);
        Assert.DoesNotContain(Roles.Auditor, Roles.FinanceRoles);
        Assert.Contains(Roles.Auditor, Roles.ReportRoles);
        Assert.Contains(Roles.Auditor, Roles.AuditRoles);
    }

    [Fact]
    public void Valet_has_no_company_wide_access()
    {
        Assert.DoesNotContain(Roles.Valet, Roles.CrossBranchRoles);
        Assert.DoesNotContain(Roles.Valet, Roles.ManageUsersRoles);
        Assert.DoesNotContain(Roles.Valet, Roles.ReportRoles);
    }
}
