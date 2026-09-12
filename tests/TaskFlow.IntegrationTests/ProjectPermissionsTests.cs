using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Core.Domain.Entities;
using TaskFlow.Core.Domain.Enums;
using TaskFlow.Data;

namespace TaskFlow.IntegrationTests;

public class ProjectPermissionsTests
{
    private const string ValidPassword = "Clave#12345";

    private static readonly Regex AntiforgeryTokenRegex = new(
        @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""",
        RegexOptions.Compiled);

    private static string NewDbPath()
        => $"/tmp/taskflow-proj-{Guid.NewGuid():N}.db";

    private static WebApplicationFactory<Program> CreateFactory(string dbPath)
        => new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseSetting("ConnectionStrings:Default", $"Data Source={dbPath}"));

    private static async Task<AppUser> CreateUserAsync(
        WebApplicationFactory<Program> factory,
        string email)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            DisplayName = "Usuario de prueba",
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, ValidPassword);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        return user;
    }

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> factory, AppUser user)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await GetFormTokenAsync(client, "/Account/Login");
        var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["Email"] = user.Email,
            ["Password"] = ValidPassword,
            ["RememberMe"] = "false",
            ["ReturnUrl"] = "",
            ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        return client;
    }

    private static async Task<string> GetFormTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = AntiforgeryTokenRegex.Match(html);
        Assert.True(match.Success, $"Token antiforgery no encontrado en {url}.");
        return match.Groups[1].Value;
    }

    private static async Task<string> GetSessionTokenAsync(HttpClient client)
        => await GetFormTokenAsync(client, "/Projects");

    private sealed record TestProject(Guid Id, AppUser Owner, AppUser Manager, AppUser Member, AppUser Viewer, AppUser Outsider, Guid MembersTask, Guid OwnersTask);

    private static async Task<TestProject> SeedProjectAsync(
        WebApplicationFactory<Program> factory,
        string slug)
    {
        var owner = await CreateUserAsync(factory, $"owner-{slug}@example.com");
        var manager = await CreateUserAsync(factory, $"manager-{slug}@example.com");
        var member = await CreateUserAsync(factory, $"member-{slug}@example.com");
        var viewer = await CreateUserAsync(factory, $"viewer-{slug}@example.com");
        var outsider = await CreateUserAsync(factory, $"outsider-{slug}@example.com");

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = $"Proyecto {slug}",
            Key = $"KEY{slug.ToUpperInvariant()}",
            Description = "Proyecto de prueba",
            CreatedByUserId = owner.Id,
            CreatedAtUtc = DateTime.UtcNow
        };

        var members = new[]
        {
            new ProjectMember { ProjectId = project.Id, UserId = owner.Id, Role = ProjectRole.Owner, JoinedAtUtc = DateTime.UtcNow },
            new ProjectMember { ProjectId = project.Id, UserId = manager.Id, Role = ProjectRole.Manager, JoinedAtUtc = DateTime.UtcNow },
            new ProjectMember { ProjectId = project.Id, UserId = member.Id, Role = ProjectRole.Member, JoinedAtUtc = DateTime.UtcNow },
            new ProjectMember { ProjectId = project.Id, UserId = viewer.Id, Role = ProjectRole.Viewer, JoinedAtUtc = DateTime.UtcNow }
        };

        var membersTask = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Title = "Tarea del miembro",
            Description = string.Empty,
            Status = TaskStatus.Todo,
            Priority = TaskPriority.Medium,
            AssigneeId = member.Id,
            CreatedByUserId = member.Id,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var ownersTask = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Title = "Tarea del owner",
            Description = string.Empty,
            Status = TaskStatus.Todo,
            Priority = TaskPriority.High,
            AssigneeId = owner.Id,
            CreatedByUserId = owner.Id,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Projects.Add(project);
        db.ProjectMembers.AddRange(members);
        db.TaskItems.AddRange(membersTask, ownersTask);
        await db.SaveChangesAsync();

        return new TestProject(project.Id, owner, manager, member, viewer, outsider, membersTask.Id, ownersTask.Id);
    }

    private static async Task<HttpResponseMessage> PostProjectDeleteAsync(
        HttpClient client,
        string token,
        Guid projectId)
        => await client.PostAsync($"/Projects/{projectId}/Delete", new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["__RequestVerificationToken"] = token
        }));

    private static async Task<HttpResponseMessage> PostTaskEditAsync(
        HttpClient client,
        string token,
        Guid taskId,
        string assigneeId)
        => await client.PostAsync($"/Tasks/{taskId}/Edit", new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["Title"] = "Tarea editada",
            ["Description"] = "",
            ["Priority"] = "1",
            ["Status"] = "1",
            ["DueDateUtc"] = "",
            ["AssigneeId"] = assigneeId,
            ["__RequestVerificationToken"] = token
        }));

    [Fact]
    public async Task Viewer_CanViewProjectDetails_ButCannotDelete()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "view");
        using var client = await LoginAsync(factory, project.Viewer);
        var token = await GetSessionTokenAsync(client);

        var details = await client.GetAsync($"/Projects/{project.Id}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);

        var delete = await PostProjectDeleteAsync(client, token, project.Id);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task DeleteProject_OnlyOwner_CanDelete()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "del");

        using var memberClient = await LoginAsync(factory, project.Member);
        using var managerClient = await LoginAsync(factory, project.Manager);
        using var ownerClient = await LoginAsync(factory, project.Owner);

        var memberToken = await GetSessionTokenAsync(memberClient);
        var managerToken = await GetSessionTokenAsync(managerClient);
        var ownerToken = await GetSessionTokenAsync(ownerClient);

        var memberDelete = await PostProjectDeleteAsync(memberClient, memberToken, project.Id);
        var managerDelete = await PostProjectDeleteAsync(managerClient, managerToken, project.Id);
        Assert.Equal(HttpStatusCode.Forbidden, memberDelete.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, managerDelete.StatusCode);

        var ownerDelete = await PostProjectDeleteAsync(ownerClient, ownerToken, project.Id);
        Assert.Equal(HttpStatusCode.Redirect, ownerDelete.StatusCode);
        Assert.Contains("/Projects", ownerDelete.Headers.Location?.ToString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Projects.AnyAsync(p => p.Id == project.Id));
    }

    [Fact]
    public async Task CrossProject_ProjectDetails_ReturnsNotFound()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var alpha = await SeedProjectAsync(factory, "alpha");
        var beta = await SeedProjectAsync(factory, "beta");
        using var client = await LoginAsync(factory, alpha.Member);

        var response = await client.GetAsync($"/Projects/{beta.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CrossProject_TaskDetails_ReturnsNotFound()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var alpha = await SeedProjectAsync(factory, "alpha");
        var beta = await SeedProjectAsync(factory, "beta");
        using var client = await LoginAsync(factory, alpha.Member);

        var task = await client.GetAsync($"/Tasks/{beta.MembersTask}");

        Assert.Equal(HttpStatusCode.NotFound, task.StatusCode);
    }

    [Fact]
    public async Task CrossProject_TaskEdit_ReturnsNotFound()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var alpha = await SeedProjectAsync(factory, "alpha");
        var beta = await SeedProjectAsync(factory, "beta");
        using var client = await LoginAsync(factory, alpha.Member);
        var token = await GetSessionTokenAsync(client);

        var response = await PostTaskEditAsync(client, token, beta.MembersTask, beta.Member.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Member_CanEditOwnTask_ButNotOthersTask()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "own");
        using var client = await LoginAsync(factory, project.Member);
        var token = await GetSessionTokenAsync(client);

        var own = await PostTaskEditAsync(client, token, project.MembersTask, "");
        Assert.Equal(HttpStatusCode.Redirect, own.StatusCode);

        var others = await PostTaskEditAsync(client, token, project.OwnersTask, "");
        Assert.Equal(HttpStatusCode.Forbidden, others.StatusCode);
    }

    [Fact]
    public async Task Manager_CanEditOthersTask()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "mgr");
        using var client = await LoginAsync(factory, project.Manager);
        var token = await GetSessionTokenAsync(client);

        var response = await PostTaskEditAsync(client, token, project.MembersTask, project.Member.Id);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task Manager_CannotChangeOwnersRole()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "role");
        using var client = await LoginAsync(factory, project.Manager);
        var token = await GetSessionTokenAsync(client);

        var response = await client.PostAsync(
            $"/Projects/{project.Id}/Members/{project.Owner.Id}/Role",
            new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["newRole"] = "1",
                ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LastOwner_CannotBeRemoved()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "last");
        using var client = await LoginAsync(factory, project.Owner);
        var token = await GetSessionTokenAsync(client);

        var response = await client.PostAsync(
            $"/Projects/{project.Id}/Members/{project.Owner.Id}/Remove",
            new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains($"/Projects/{project.Id}", response.Headers.Location?.ToString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stillOwner = await db.ProjectMembers
            .AnyAsync(m => m.ProjectId == project.Id && m.UserId == project.Owner.Id && m.Role == ProjectRole.Owner);
        Assert.True(stillOwner);
    }

    [Fact]
    public async Task CreateProject_MakesCreatorOwner()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var creator = await CreateUserAsync(factory, "creator@example.com");
        using var client = await LoginAsync(factory, creator);
        var token = await GetFormTokenAsync(client, "/Projects");

        var response = await client.PostAsync("/Projects", new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["Name"] = "Nuevo proyecto",
            ["Key"] = "NPX",
            ["Description"] = "Descripción",
            ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var project = await db.Projects.SingleAsync(p => p.Key == "NPX");
        var role = await db.ProjectMembers.SingleAsync(m => m.ProjectId == project.Id && m.UserId == creator.Id);
        Assert.Equal(ProjectRole.Owner, role.Role);
    }

    [Fact]
    public async Task CreateTask_AssigningNonMember_IsRejected()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "assign");
        using var client = await LoginAsync(factory, project.Member);
        var token = await GetSessionTokenAsync(client);

        var response = await client.PostAsync(
            $"/Projects/{project.Id}/Tasks",
            new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["Title"] = "Tarea con asignación inválida",
                ["Description"] = "",
                ["Priority"] = "0",
                ["Status"] = "0",
                ["DueDateUtc"] = "",
                ["AssigneeId"] = project.Outsider.Id,
                ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains($"/Projects/{project.Id}", response.Headers.Location?.ToString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.TaskItems.AnyAsync(t => t.Title == "Tarea con asignación inválida"));
    }
}