using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models.Tasks;

public sealed class AddTaskCommentViewModel
{
    [Required(ErrorMessage = "El comentario es obligatorio.")]
    [StringLength(4000, ErrorMessage = "El comentario no puede superar los 4000 caracteres.")]
    [Display(Name = "Comentario")]
    public string Body { get; set; } = string.Empty;
}