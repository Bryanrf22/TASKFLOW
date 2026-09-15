using TaskFlow.Core.Domain.Enums;

namespace TaskFlow.Core.Domain.Rules;

public static class TaskStatusTransitions
{
    private static readonly IReadOnlyDictionary<TaskStatus, TaskStatus[]> Allowed =
        new Dictionary<TaskStatus, TaskStatus[]>
        {
            [TaskStatus.Todo] = [TaskStatus.InProgress, TaskStatus.Blocked, TaskStatus.Done],
            [TaskStatus.InProgress] = [TaskStatus.Todo, TaskStatus.Blocked, TaskStatus.Done],
            [TaskStatus.Blocked] = [TaskStatus.Todo, TaskStatus.InProgress, TaskStatus.Done],
            [TaskStatus.Done] = [TaskStatus.InProgress, TaskStatus.Todo]
        };

    public static bool CanTransition(TaskStatus from, TaskStatus to)
        => from == to || Allowed.TryGetValue(from, out var allowed) && allowed.Contains(to);
}