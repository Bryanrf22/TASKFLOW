using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models.Projects;

public sealed class EditProjectViewModel
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar los 120 caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "La descripción no puede superar los 2000 caracteres.")]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }
}