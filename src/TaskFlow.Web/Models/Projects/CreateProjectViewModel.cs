using System.ComponentModel.DataAnnotations;
using TaskFlow.Core.Domain.Enums;

namespace TaskFlow.Web.Models.Projects;

public sealed class CreateProjectViewModel
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar los 120 caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "La clave es obligatoria.")]
    [RegularExpression("^[A-Za-z][A-Za-z0-9]{1,9}$", ErrorMessage = "La clave debe tener entre 2 y 10 caracteres alfanuméricos y empezar por una letra.")]
    [Display(Name = "Clave")]
    public string Key { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "La descripción no puede superar los 2000 caracteres.")]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }
}