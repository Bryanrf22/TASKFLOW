using TaskFlow.Core.Domain.Enums;

namespace TaskFlow.Core.Domain.Entities;

public sealed class ProjectMember
{
    public Guid ProjectId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ProjectRole Role { get; set; }
    public DateTime JoinedAtUtc { get; set; }
}