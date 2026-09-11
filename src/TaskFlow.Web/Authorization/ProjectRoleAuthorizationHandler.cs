using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using TaskFlow.Core.Domain.Abstractions;
using TaskFlow.Core.Domain.Authorization;
using TaskFlow.Core.Domain.Rules;

namespace TaskFlow.Web.Authorization;

public sealed class ProjectRoleAuthorizationHandler : AuthorizationHandler<RequireProjectRoleRequirement, HttpContext>
{
    private readonly IProjectMembershipReader _memberships;

    public ProjectRoleAuthorizationHandler(IProjectMembershipReader memberships)
    {
        _memberships = memberships;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequireProjectRoleRequirement requirement,
        HttpContext resource)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return;

        if (context.User.IsInRole(ApplicationRoles.Admin))
        {
            context.Succeed(requirement);
            return;
        }

        if (!resource.Request.RouteValues.TryGetValue("projectId", out var rawId) || rawId is null)
            return;

        if (!Guid.TryParse(rawId.ToString(), out var projectId))
            return;

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return;

        var role = await _memberships.GetRoleAsync(userId, projectId);
        if (role.HasValue && Permissions.AtLeast(role.Value, requirement.MinimumRole))
            context.Succeed(requirement);
    }
}