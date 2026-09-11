namespace TaskFlow.Web.Models.Admin;

public sealed class UsersListViewModel
{
    public IReadOnlyList<UserListItemViewModel> Users { get; set; } = Array.Empty<UserListItemViewModel>();
}