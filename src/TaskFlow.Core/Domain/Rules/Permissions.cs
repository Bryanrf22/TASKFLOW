using TaskFlow.Core.Domain.Enums;

namespace TaskFlow.Core.Domain.Rules;

public static class Permissions
{
    public static bool CanManageProject(ProjectRole role)
        => role == ProjectRole.Manager;

    public static bool AtLeast(ProjectRole actual, ProjectRole required)
        => actual >= required;
}