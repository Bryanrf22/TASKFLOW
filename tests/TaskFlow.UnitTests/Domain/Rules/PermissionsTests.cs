using TaskFlow.Core.Domain.Enums;
using TaskFlow.Core.Domain.Rules;

namespace TaskFlow.UnitTests.Domain.Rules;

public class PermissionsTests
{
    [Theory]
    [InlineData(ProjectRole.Viewer, ProjectRole.Viewer, true)]
    [InlineData(ProjectRole.Viewer, ProjectRole.Member, false)]
    [InlineData(ProjectRole.Member, ProjectRole.Member, true)]
    [InlineData(ProjectRole.Member, ProjectRole.Manager, false)]
    [InlineData(ProjectRole.Manager, ProjectRole.Member, true)]
    [InlineData(ProjectRole.Manager, ProjectRole.Owner, false)]
    [InlineData(ProjectRole.Owner, ProjectRole.Owner, true)]
    [InlineData(ProjectRole.Owner, ProjectRole.Viewer, true)]
    public void IsAtLeast_EvaluatesRoleHierarchy(ProjectRole actual, ProjectRole required, bool expected)
    {
        Assert.Equal(expected, Permissions.IsAtLeast(actual, required));
    }

    [Theory]
    [InlineData(ProjectRole.Viewer, true)]
    [InlineData(ProjectRole.Member, true)]
    [InlineData(ProjectRole.Manager, true)]
    [InlineData(ProjectRole.Owner, true)]
    public void CanViewProject_AllowsEveryRole(ProjectRole role, bool expected)
    {
        Assert.Equal(expected, Permissions.CanViewProject(role));
    }

    [Theory]
    [InlineData(ProjectRole.Viewer, false)]
    [InlineData(ProjectRole.Member, true)]
    [InlineData(ProjectRole.Manager, true)]
    [InlineData(ProjectRole.Owner, true)]
    public void CanCreateTask_RequiresMember(ProjectRole role, bool expected)
    {
        Assert.Equal(expected, Permissions.CanCreateTask(role));
    }

    [Theory]
    [InlineData(ProjectRole.Viewer, false)]
    [InlineData(ProjectRole.Member, false)]
    [InlineData(ProjectRole.Manager, true)]
    [InlineData(ProjectRole.Owner, true)]
    public void CanEditAnyTask_RequiresManager(ProjectRole role, bool expected)
    {
        Assert.Equal(expected, Permissions.CanEditAnyTask(role));
    }

    [Theory]
    [InlineData(ProjectRole.Viewer, false)]
    [InlineData(ProjectRole.Member, false)]
    [InlineData(ProjectRole.Manager, true)]
    [InlineData(ProjectRole.Owner, true)]
    public void CanDeleteTask_RequiresManager(ProjectRole role, bool expected)
    {
        Assert.Equal(expected, Permissions.CanDeleteTask(role));
    }

    [Theory]
    [InlineData(ProjectRole.Viewer, false)]
    [InlineData(ProjectRole.Member, true)]
    [InlineData(ProjectRole.Manager, true)]
    [InlineData(ProjectRole.Owner, true)]
    public void CanComment_RequiresMember(ProjectRole role, bool expected)
    {
        Assert.Equal(expected, Permissions.CanComment(role));
    }

    [Theory]
    [InlineData(ProjectRole.Viewer, true, false)]
    [InlineData(ProjectRole.Member, true, true)]
    [InlineData(ProjectRole.Member, false, false)]
    [InlineData(ProjectRole.Manager, true, true)]
    [InlineData(ProjectRole.Manager, false, true)]
    [InlineData(ProjectRole.Owner, true, true)]
    [InlineData(ProjectRole.Owner, false, true)]
    public void CanEditTask_MemberEditsOnlyOwnTask(ProjectRole role, bool isOwnTask, bool expected)
    {
        Assert.Equal(expected, Permissions.CanEditTask(role, isOwnTask));
    }

    [Theory]
    [InlineData(ProjectRole.Viewer, false)]
    [InlineData(ProjectRole.Member, false)]
    [InlineData(ProjectRole.Manager, true)]
    [InlineData(ProjectRole.Owner, true)]
    public void CanManageMembers_RequiresManager(ProjectRole role, bool expected)
    {
        Assert.Equal(expected, Permissions.CanManageMembers(role));
    }

    [Theory]
    [InlineData(ProjectRole.Manager, ProjectRole.Member, true)]
    [InlineData(ProjectRole.Manager, ProjectRole.Manager, true)]
    [InlineData(ProjectRole.Manager, ProjectRole.Owner, false)]
    [InlineData(ProjectRole.Owner, ProjectRole.Owner, false)]
    [InlineData(ProjectRole.Owner, ProjectRole.Manager, true)]
    public void CanModifyMember_ManagerCannotTouchOwners(ProjectRole actor, ProjectRole targetRole, bool expected)
    {
        Assert.Equal(expected, Permissions.CanModifyMember(actor, targetRole));
    }

    [Theory]
    [InlineData(ProjectRole.Viewer, false)]
    [InlineData(ProjectRole.Member, false)]
    [InlineData(ProjectRole.Manager, false)]
    [InlineData(ProjectRole.Owner, true)]
    public void CanGrantOwner_OnlyOwner(ProjectRole role, bool expected)
    {
        Assert.Equal(expected, Permissions.CanGrantOwner(role));
    }

    [Theory]
    [InlineData(ProjectRole.Viewer, false)]
    [InlineData(ProjectRole.Member, false)]
    [InlineData(ProjectRole.Manager, true)]
    [InlineData(ProjectRole.Owner, true)]
    public void CanManageProjectSettings_RequiresManager(ProjectRole role, bool expected)
    {
        Assert.Equal(expected, Permissions.CanManageProjectSettings(role));
    }

    [Theory]
    [InlineData(ProjectRole.Viewer, false)]
    [InlineData(ProjectRole.Member, false)]
    [InlineData(ProjectRole.Manager, false)]
    [InlineData(ProjectRole.Owner, true)]
    public void CanDeleteProject_OnlyOwner(ProjectRole role, bool expected)
    {
        Assert.Equal(expected, Permissions.CanDeleteProject(role));
    }
}