using Microsoft.AspNetCore.Authorization;
using TaskFlow.Core.Domain.Enums;

namespace TaskFlow.Web.Authorization;

public sealed class RequireProjectRoleRequirement : IAuthorizationRequirement
{
    public RequireProjectRoleRequirement(ProjectRole minimumRole)
    {
        MinimumRole = minimumRole;
    }

    public ProjectRole MinimumRole { get; }
}