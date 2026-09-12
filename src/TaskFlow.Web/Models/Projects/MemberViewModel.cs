using TaskFlow.Core.Domain.Enums;

namespace TaskFlow.Web.Models.Projects;

public sealed class MemberViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ProjectRole Role { get; set; }
    public DateTime JoinedAtUtc { get; set; }
}