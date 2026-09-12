using TaskFlow.Core.Domain.Enums;

namespace TaskFlow.Web.Models.Projects;

public sealed class ProjectListItemViewModel
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public ProjectRole MyRole { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}