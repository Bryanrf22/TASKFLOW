using TaskFlow.Core.Domain.Enums;

namespace TaskFlow.Web.Models.Tasks;

public sealed class TaskListItemViewModel
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public TaskStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public string? AssigneeEmail { get; set; }
}