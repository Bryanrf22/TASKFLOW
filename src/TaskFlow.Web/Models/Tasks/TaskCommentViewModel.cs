namespace TaskFlow.Web.Models.Tasks;

public sealed class TaskCommentViewModel
{
    public Guid Id { get; set; }
    public string Body { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}