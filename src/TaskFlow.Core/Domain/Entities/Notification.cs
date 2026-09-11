using TaskFlow.Core.Domain.Enums;

namespace TaskFlow.Core.Domain.Entities;

public sealed class Notification
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}