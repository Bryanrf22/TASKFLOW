namespace TaskFlow.Core.Domain.Entities;

public sealed class TaskLabel
{
    public Guid TaskId { get; set; }
    public Guid LabelId { get; set; }
}