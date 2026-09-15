namespace TaskFlow.Core.Domain.Entities;

public sealed class TaskComment
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string AuthorUserId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}