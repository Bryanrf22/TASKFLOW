using TaskFlow.Core.Domain.Enums;

namespace TaskFlow.UnitTests.Domain.Enums;

public class EnumDefinitionsTests
{
    [Fact]
    public void TaskStatus_Defines_ExpectedStates()
    {
        var statuses = Enum.GetValues<TaskStatus>();

        Assert.Equal(4, statuses.Length);
        Assert.Contains(TaskStatus.Todo, statuses);
        Assert.Contains(TaskStatus.InProgress, statuses);
        Assert.Contains(TaskStatus.Blocked, statuses);
        Assert.Contains(TaskStatus.Done, statuses);
    }

    [Fact]
    public void TaskPriority_Defines_ExpectedPriorities()
    {
        var priorities = Enum.GetValues<TaskPriority>();

        Assert.Equal(4, priorities.Length);
        Assert.Contains(TaskPriority.Low, priorities);
        Assert.Contains(TaskPriority.Medium, priorities);
        Assert.Contains(TaskPriority.High, priorities);
        Assert.Contains(TaskPriority.Urgent, priorities);
    }

    [Fact]
    public void ProjectRole_Manager_Outranks_Member()
    {
        Assert.True(ProjectRole.Manager > ProjectRole.Member);
    }

    [Fact]
    public void NotificationType_Defines_ExpectedEvents()
    {
        var types = Enum.GetValues<NotificationType>();

        Assert.Contains(NotificationType.TaskAssigned, types);
        Assert.Contains(NotificationType.Mention, types);
        Assert.Contains(NotificationType.CommentAdded, types);
        Assert.Contains(NotificationType.TaskStatusChanged, types);
        Assert.Contains(NotificationType.DueSoon, types);
    }
}