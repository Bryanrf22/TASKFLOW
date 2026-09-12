using System.ComponentModel.DataAnnotations;
using TaskFlow.Core.Domain.Enums;
using TaskFlow.Web.Models.Projects;

namespace TaskFlow.Web.Models.Tasks;

public sealed class CreateTaskViewModel
{
    [Required(ErrorMessage = "El título es obligatorio.")]
    [StringLength(200, ErrorMessage = "El título no puede superar los 200 caracteres.")]
    [Display(Name = "Título")]
    public string Title { get; set; } = string.Empty;

    [StringLength(5000, ErrorMessage = "La descripción no puede superar los 5000 caracteres.")]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }

    [Display(Name = "Prioridad")]
    [EnumDataType(typeof(TaskPriority), ErrorMessage = "La prioridad no es válida.")]
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    [Display(Name = "Estado")]
    [EnumDataType(typeof(TaskStatus), ErrorMessage = "El estado no es válido.")]
    public TaskStatus Status { get; set; } = TaskStatus.Todo;

    [DataType(DataType.Date)]
    [Display(Name = "Vencimiento")]
    public DateTime? DueDateUtc { get; set; }

    [Display(Name = "Asignado a")]
    public string? AssigneeId { get; set; }

    public IReadOnlyList<MemberViewModel> AssignableMembers { get; set; } = Array.Empty<MemberViewModel>();
}