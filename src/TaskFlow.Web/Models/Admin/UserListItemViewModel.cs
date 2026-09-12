namespace TaskFlow.Web.Models.Admin;

public sealed class UserListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public bool IsAdministrator { get; set; }
}