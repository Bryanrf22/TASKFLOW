using TaskFlow.Web.Models.Projects;

namespace TaskFlow.Web.Models.Projects;

public sealed class ProjectsIndexViewModel
{
    public IReadOnlyList<ProjectListItemViewModel> Projects { get; set; } = Array.Empty<ProjectListItemViewModel>();
    public CreateProjectViewModel Create { get; set; } = new();
}