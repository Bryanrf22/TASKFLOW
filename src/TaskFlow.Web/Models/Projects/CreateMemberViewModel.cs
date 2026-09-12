using System.ComponentModel.DataAnnotations;
using TaskFlow.Core.Domain.Enums;

namespace TaskFlow.Web.Models.Projects;

public sealed class CreateMemberViewModel
{
    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El rol es obligatorio.")]
    [EnumDataType(typeof(ProjectRole), ErrorMessage = "El rol no es válido.")]
    [Display(Name = "Rol")]
    public ProjectRole Role { get; set; } = ProjectRole.Member;
}