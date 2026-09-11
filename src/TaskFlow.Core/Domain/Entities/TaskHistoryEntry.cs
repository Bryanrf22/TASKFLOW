using TaskFlow.Core.Domain.Enums;

namespace TaskFlow.Core.Domain.Entities;

public sealed class TaskHistoryEntry
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public ChangeType ChangeType { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string? Field { get; set; }
    public string? FromValue { get; set; }
    public string? ToValue { get; set; }
    public DateTime AtUtc { get; set; }
}