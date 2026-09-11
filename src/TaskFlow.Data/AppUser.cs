using Microsoft.AspNetCore.Identity;

namespace TaskFlow.Data;

public sealed class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}