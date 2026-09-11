using TaskFlow.Core.Domain.Enums;
using TaskFlow.Core.Domain.Rules;

namespace TaskFlow.UnitTests.Domain.Rules;

public class PermissionsTests
{
    [Theory]
    [InlineData(ProjectRole.Manager, true)]
    [InlineData(ProjectRole.Member, false)]
    public void CanManageProject_ReturnsTrueOnlyForManager(ProjectRole role, bool expected)
    {
        Assert.Equal(expected, Permissions.CanManageProject(role));
    }

    [Theory]
    [InlineData(ProjectRole.Member, ProjectRole.Member, true)]
    [InlineData(ProjectRole.Manager, ProjectRole.Manager, true)]
    [InlineData(ProjectRole.Manager, ProjectRole.Member, true)]
    [InlineData(ProjectRole.Member, ProjectRole.Manager, false)]
    public void AtLeast_EvaluatesRoleHierarchy(ProjectRole actual, ProjectRole required, bool expected)
    {
        Assert.Equal(expected, Permissions.AtLeast(actual, required));
    }
}