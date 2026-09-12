using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using TaskFlow.Core.Domain.Abstractions;
using TaskFlow.Core.Domain.Authorization;
using TaskFlow.Core.Domain.Enums;
using TaskFlow.Web.Authorization;

namespace TaskFlow.UnitTests.Authorization;

public class ProjectRoleAuthorizationHandlerTests
{
    private sealed class FakeMembershipReader : IProjectMembershipReader
    {
        private readonly Dictionary<string, ProjectRole> _roles = new();

        public void SetRole(string userId, ProjectRole role)
        {
            _roles[userId] = role;
        }

        public Task<ProjectRole?> GetRoleAsync(string userId, Guid projectId, CancellationToken cancellationToken = default)
        {
            ProjectRole? role = _roles.TryGetValue(userId, out var value) ? value : null;
            return Task.FromResult(role);
        }
    }

    [Fact]
    public async Task Unauthenticated_Fails()
    {
        var handler = new ProjectRoleAuthorizationHandler(new FakeMembershipReader());
        var context = CreateContext(CreatePrincipal(null), projectId: null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task AuthenticatedWithoutMembership_Fails()
    {
        var handler = new ProjectRoleAuthorizationHandler(new FakeMembershipReader());
        var context = CreateContext(CreatePrincipal("user-1"), projectId: Guid.NewGuid());

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task MemberRequiresManager_Fails()
    {
        var reader = new FakeMembershipReader();
        reader.SetRole("user-1", ProjectRole.Member);
        var handler = new ProjectRoleAuthorizationHandler(reader);
        var context = CreateContext(
            CreatePrincipal("user-1"),
            projectId: Guid.NewGuid(),
            requirement: new RequireProjectRoleRequirement(ProjectRole.Manager));

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task MemberRequiresMember_Succeeds()
    {
        var reader = new FakeMembershipReader();
        reader.SetRole("user-1", ProjectRole.Member);
        var handler = new ProjectRoleAuthorizationHandler(reader);
        var context = CreateContext(
            CreatePrincipal("user-1"),
            projectId: Guid.NewGuid(),
            requirement: new RequireProjectRoleRequirement(ProjectRole.Member));

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task ManagerRequiresManager_Succeeds()
    {
        var reader = new FakeMembershipReader();
        reader.SetRole("user-1", ProjectRole.Manager);
        var handler = new ProjectRoleAuthorizationHandler(reader);
        var context = CreateContext(
            CreatePrincipal("user-1"),
            projectId: Guid.NewGuid(),
            requirement: new RequireProjectRoleRequirement(ProjectRole.Manager));

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task AdminBypassesProjectMembership()
    {
        var handler = new ProjectRoleAuthorizationHandler(new FakeMembershipReader());
        var context = CreateContext(
            CreatePrincipal("user-1", isAdmin: true),
            projectId: Guid.NewGuid(),
            requirement: new RequireProjectRoleRequirement(ProjectRole.Manager));

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task MissingProjectRouteValue_Fails()
    {
        var reader = new FakeMembershipReader();
        reader.SetRole("user-1", ProjectRole.Manager);
        var handler = new ProjectRoleAuthorizationHandler(reader);
        var context = CreateContext(CreatePrincipal("user-1"), projectId: null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task InvalidProjectRouteValue_Fails()
    {
        var reader = new FakeMembershipReader();
        reader.SetRole("user-1", ProjectRole.Manager);
        var handler = new ProjectRoleAuthorizationHandler(reader);
        var context = CreateContext(CreatePrincipal("user-1"), projectId: "not-a-guid");

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    private static AuthorizationHandlerContext CreateContext(
        ClaimsPrincipal principal,
        object? projectId,
        IAuthorizationRequirement? requirement = null)
    {
        var httpContext = new DefaultHttpContext();
        if (projectId is not null)
        {
            httpContext.Request.RouteValues["projectId"] = projectId.ToString();
        }

        return new AuthorizationHandlerContext(
            new[] { requirement ?? new RequireProjectRoleRequirement(ProjectRole.Member) },
            principal,
            httpContext);
    }

    private static ClaimsPrincipal CreatePrincipal(string? userId, bool isAdmin = false)
    {
        var claims = new List<Claim>();
        if (userId is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        }
        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, ApplicationRoles.Admin));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}