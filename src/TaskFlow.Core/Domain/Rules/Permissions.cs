using TaskFlow.Core.Domain.Enums;

namespace TaskFlow.Core.Domain.Rules;

public static class Permissions
{
    public static bool IsAtLeast(ProjectRole actual, ProjectRole required)
        => actual >= required;

    public static bool CanViewProject(ProjectRole role)
        => true;

    public static bool CanReadMembers(ProjectRole role)
        => true;

    public static bool CanCreateTask(ProjectRole role)
        => IsAtLeast(role, ProjectRole.Member);

    public static bool CanEditAnyTask(ProjectRole role)
        => IsAtLeast(role, ProjectRole.Manager);

    public static bool CanDeleteTask(ProjectRole role)
        => IsAtLeast(role, ProjectRole.Manager);

    public static bool CanComment(ProjectRole role)
        => IsAtLeast(role, ProjectRole.Member);

    public static bool CanEditTask(ProjectRole role, bool isOwnTask)
        => isOwnTask ? CanCreateTask(role) : CanEditAnyTask(role);

    public static bool CanManageMembers(ProjectRole role)
        => IsAtLeast(role, ProjectRole.Manager);

    public static bool CanModifyMember(ProjectRole actor, ProjectRole targetRole)
        => CanManageMembers(actor) && (targetRole != ProjectRole.Owner || CanGrantOwner(actor));

    public static bool CanGrantOwner(ProjectRole actor)
        => actor == ProjectRole.Owner;

    public static bool CanManageProjectSettings(ProjectRole role)
        => IsAtLeast(role, ProjectRole.Manager);

    public static bool CanDeleteProject(ProjectRole role)
        => role == ProjectRole.Owner;
}