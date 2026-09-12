using TaskFlow.Core.Domain.Entities;
using TaskFlow.Core.Domain.Enums;
using TaskFlow.Web.Models.Tasks;

namespace TaskFlow.Web.Models.Projects;

public sealed class ProjectDetailsViewModel
{
    public Project Project { get; set; } = new();
    public ProjectRole MyRole { get; set; }
    public bool CanEditSettings { get; set; }
    public bool CanDelete { get; set; }
    public bool CanManageMembers { get; set; }
    public bool CanCreateTask { get; set; }

    public IReadOnlyList<MemberViewModel> Members { get; set; } = Array.Empty<MemberViewModel>();
    public IReadOnlyList<MemberViewModel> AssignableMembers { get; set; } = Array.Empty<MemberViewModel>();
    public IReadOnlyList<TaskListItemViewModel> Tasks { get; set; } = Array.Empty<TaskListItemViewModel>();

    public CreateMemberViewModel CreateMember { get; set; } = new();
    public CreateTaskViewModel CreateTask { get; set; } = new();
    public EditProjectViewModel EditModel { get; set; } = new();
}