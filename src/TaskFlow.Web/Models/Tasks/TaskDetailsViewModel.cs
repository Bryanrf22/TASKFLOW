using TaskFlow.Core.Domain.Enums;
using TaskFlow.Core.Domain.Entities;
using TaskFlow.Web.Models.Projects;

namespace TaskFlow.Web.Models.Tasks;

public sealed class TaskDetailsViewModel
{
    public TaskItem Task { get; set; } = new();
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? AssigneeName { get; set; }
    public ProjectRole MyRole { get; set; }
    public bool OwnsTask { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanComment { get; set; }

    public EditTaskViewModel EditModel { get; set; } = new();

    public IReadOnlyList<TaskCommentViewModel> Comments { get; set; } = Array.Empty<TaskCommentViewModel>();

    public IReadOnlyList<MemberViewModel> AssignableMembers { get; set; } = Array.Empty<MemberViewModel>();
}