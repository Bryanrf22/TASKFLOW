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

public class ProjectLifecycleTests
{
    private const string ValidPassword = "Clave#12345";

    private static readonly Regex AntiforgeryTokenRegex = new(
        @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""",
        RegexOptions.Compiled);

    private static string NewDbPath()
        => $"/tmp/taskflow-life-{Guid.NewGuid():N}.db";

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

    private sealed record TestProject(Guid Id, AppUser Owner, AppUser Manager, AppUser Member, AppUser Viewer, AppUser Outsider, Guid MembersTask);

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
            Key = ("KEY" + slug.ToUpperInvariant())[..Math.Min(10, 3 + slug.Length)],
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

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Projects.Add(project);
        db.ProjectMembers.AddRange(members);
        db.TaskItems.Add(membersTask);
        await db.SaveChangesAsync();

        return new TestProject(project.Id, owner, manager, member, viewer, outsider, membersTask.Id);
    }

    private static FormUrlEncodedContent PostData(IDictionary<string, string?> values, string token)
    {
        var merged = new Dictionary<string, string?>(values)
        {
            ["__RequestVerificationToken"] = token
        };
        return new FormUrlEncodedContent(merged);
    }

    private const string NewMemberEmail = "nuevo.miembro@example.com";

    [Fact]
    public async Task AddMember_Success_GrantsAccess()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "adds");
        var newMember = await CreateUserAsync(factory, NewMemberEmail);
        using var client = await LoginAsync(factory, project.Manager);
        var token = await GetSessionTokenAsync(client);

        var response = await client.PostAsync(
            $"/Projects/{project.Id}/Members/Add",
            PostData(new Dictionary<string, string?>
            {
                ["Email"] = NewMemberEmail,
                ["Role"] = "1"
            }, token));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains($"/Projects/{project.Id}", response.Headers.Location?.ToString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var membership = await db.ProjectMembers.SingleAsync(m => m.ProjectId == project.Id && m.UserId == newMember.Id);
        Assert.Equal(ProjectRole.Member, membership.Role);

        using var newClient = await LoginAsync(factory, newMember);
        var details = await newClient.GetAsync($"/Projects/{project.Id}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
    }

    [Fact]
    public async Task AddMember_Duplicate_RedirectsWithError()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "dupm");
        using var client = await LoginAsync(factory, project.Manager);
        var token = await GetSessionTokenAsync(client);

        var response = await client.PostAsync(
            $"/Projects/{project.Id}/Members/Add",
            PostData(new Dictionary<string, string?>
            {
                ["Email"] = project.Member.Email!,
                ["Role"] = "1"
            }, token));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.ProjectMembers.CountAsync(m => m.ProjectId == project.Id && m.UserId == project.Member.Id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task AddMember_UnknownEmail_RedirectsWithError_NoMembership()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "unk");
        using var client = await LoginAsync(factory, project.Manager);
        var token = await GetSessionTokenAsync(client);

        var response = await client.PostAsync(
            $"/Projects/{project.Id}/Members/Add",
            PostData(new Dictionary<string, string?>
            {
                ["Email"] = "nadie@example.com",
                ["Role"] = "1"
            }, token));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains($"/Projects/{project.Id}", response.Headers.Location?.ToString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(4, await db.ProjectMembers.CountAsync(m => m.ProjectId == project.Id));
    }

    [Fact]
    public async Task AddMember_ManagerCannotGrantOwner_ReturnsForbidden()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "mgrowner");
        var newMember = await CreateUserAsync(factory, NewMemberEmail);
        using var client = await LoginAsync(factory, project.Manager);
        var token = await GetSessionTokenAsync(client);

        var response = await client.PostAsync(
            $"/Projects/{project.Id}/Members/Add",
            PostData(new Dictionary<string, string?>
            {
                ["Email"] = NewMemberEmail,
                ["Role"] = "3"
            }, token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.ProjectMembers.AnyAsync(m => m.ProjectId == project.Id && m.UserId == newMember.Id));
    }

    [Fact]
    public async Task AddMember_OutOfRangeRole_IsRejected_NoMembership()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "badd");
        var newMember = await CreateUserAsync(factory, NewMemberEmail);
        using var client = await LoginAsync(factory, project.Manager);
        var token = await GetSessionTokenAsync(client);

        var response = await client.PostAsync(
            $"/Projects/{project.Id}/Members/Add",
            PostData(new Dictionary<string, string?>
            {
                ["Email"] = NewMemberEmail,
                ["Role"] = "99"
            }, token));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.ProjectMembers.AnyAsync(m => m.ProjectId == project.Id && m.UserId == newMember.Id));
    }

    [Fact]
    public async Task ChangeMemberRole_Success_Persists()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "promote");
        using var client = await LoginAsync(factory, project.Manager);
        var token = await GetSessionTokenAsync(client);

        var response = await client.PostAsync(
            $"/Projects/{project.Id}/Members/{project.Member.Id}/Role",
            PostData(new Dictionary<string, string?>
            {
                ["newRole"] = "2"
            }, token));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var membership = await db.ProjectMembers.SingleAsync(m => m.ProjectId == project.Id && m.UserId == project.Member.Id);
        Assert.Equal(ProjectRole.Manager, membership.Role);
    }

    [Fact]
    public async Task ChangeMemberRole_OutOfRange_ReturnsBadRequest_NoChange()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "badenum");
        using var client = await LoginAsync(factory, project.Manager);
        var token = await GetSessionTokenAsync(client);

        var response = await client.PostAsync(
            $"/Projects/{project.Id}/Members/{project.Member.Id}/Role",
            PostData(new Dictionary<string, string?>
            {
                ["newRole"] = "99"
            }, token));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var membership = await db.ProjectMembers.SingleAsync(m => m.ProjectId == project.Id && m.UserId == project.Member.Id);
        Assert.Equal(ProjectRole.Member, membership.Role);
    }

    [Fact]
    public async Task Manager_CannotGrantOwner_Role()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "noowner");
        using var client = await LoginAsync(factory, project.Manager);
        var token = await GetSessionTokenAsync(client);

        var response = await client.PostAsync(
            $"/Projects/{project.Id}/Members/{project.Member.Id}/Role",
            PostData(new Dictionary<string, string?>
            {
                ["newRole"] = "3"
            }, token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Owner_GrantsSecondOwner_ThenCanDemoteIt()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "second");
        using var client = await LoginAsync(factory, project.Owner);
        var token = await GetSessionTokenAsync(client);

        var grant = await client.PostAsync(
            $"/Projects/{project.Id}/Members/{project.Viewer.Id}/Role",
            PostData(new Dictionary<string, string?>
            {
                ["newRole"] = "3"
            }, token));

        var grantBody = await grant.Content.ReadAsStringAsync();
        Assert.True(
            grant.StatusCode == HttpStatusCode.Redirect,
            $"grant={grant.StatusCode} location={grant.Headers.Location} body={grantBody}");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var viewerRole = await db.ProjectMembers
            .Where(m => m.ProjectId == project.Id && m.UserId == project.Viewer.Id)
            .Select(m => m.Role)
            .SingleAsync();
        Assert.Equal(ProjectRole.Owner, viewerRole);

        var ownerCount = await db.ProjectMembers.CountAsync(m => m.ProjectId == project.Id && m.Role == ProjectRole.Owner);
        Assert.Equal(2, ownerCount);

        var demote = await client.PostAsync(
            $"/Projects/{project.Id}/Members/{project.Viewer.Id}/Role",
            PostData(new Dictionary<string, string?>
            {
                ["newRole"] = "1"
            }, token));

        Assert.Equal(HttpStatusCode.Redirect, demote.StatusCode);

        var demotedRole = await db.ProjectMembers
            .Where(m => m.ProjectId == project.Id && m.UserId == project.Viewer.Id)
            .Select(m => m.Role)
            .SingleAsync();
        Assert.Equal(ProjectRole.Member, demotedRole);

        var ownerCountAfter = await db.ProjectMembers.CountAsync(m => m.ProjectId == project.Id && m.Role == ProjectRole.Owner);
        Assert.Equal(1, ownerCountAfter);
    }

    [Fact]
    public async Task RemoveMember_TerminatesAccess_AndUnassignsTasks()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "remmove");
        using var client = await LoginAsync(factory, project.Manager);
        var token = await GetSessionTokenAsync(client);

        var response = await client.PostAsync(
            $"/Projects/{project.Id}/Members/{project.Member.Id}/Remove",
            PostData(new Dictionary<string, string?>(),
                token));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.ProjectMembers.AnyAsync(m => m.ProjectId == project.Id && m.UserId == project.Member.Id));
        var task = await db.TaskItems.SingleAsync(t => t.Id == project.MembersTask);
        Assert.Null(task.AssigneeId);

        using var removedClient = await LoginAsync(factory, project.Member);
        var details = await removedClient.GetAsync($"/Projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NotFound, details.StatusCode);
        var taskDetails = await removedClient.GetAsync($"/Tasks/{project.MembersTask}");
        Assert.Equal(HttpStatusCode.NotFound, taskDetails.StatusCode);
    }

    [Fact]
    public async Task Owner_CanRemoveSelf_WhenAnotherOwnerExists()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "selfrem");
        using var client = await LoginAsync(factory, project.Owner);
        var token = await GetSessionTokenAsync(client);

        await client.PostAsync(
            $"/Projects/{project.Id}/Members/{project.Viewer.Id}/Role",
            PostData(new Dictionary<string, string?> { ["newRole"] = "3" }, token));

        var response = await client.PostAsync(
            $"/Projects/{project.Id}/Members/{project.Owner.Id}/Remove",
            PostData(new Dictionary<string, string?>(),
                token));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.ProjectMembers.AnyAsync(m => m.ProjectId == project.Id && m.UserId == project.Owner.Id));
        var remainingOwner = await db.ProjectMembers
            .SingleAsync(m => m.ProjectId == project.Id && m.Role == ProjectRole.Owner);
        Assert.Equal(project.Viewer.Id, remainingOwner.UserId);
    }

    [Fact]
    public async Task CreateTask_Success_PersistsAndRedirects()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "ctask");
        using var client = await LoginAsync(factory, project.Member);
        var token = await GetSessionTokenAsync(client);
        var dueDate = new DateTime(2026, 12, 31);

        var response = await client.PostAsync(
            $"/Projects/{project.Id}/Tasks",
            PostData(new Dictionary<string, string?>
            {
                ["Title"] = "Nueva tarea",
                ["Description"] = "Descripción de la tarea",
                ["Priority"] = "2",
                ["Status"] = "1",
                ["DueDateUtc"] = "2026-12-31",
                ["AssigneeId"] = project.Viewer.Id
            }, token));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Tasks/", response.Headers.Location?.ToString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var task = await db.TaskItems.SingleAsync(t => t.Title == "Nueva tarea");
        Assert.Equal(project.Id, task.ProjectId);
        Assert.Equal(TaskPriority.High, task.Priority);
        Assert.Equal(TaskStatus.InProgress, task.Status);
        Assert.Equal(dueDate, task.DueDateUtc);
        Assert.Equal(project.Viewer.Id, task.AssigneeId);
        Assert.Equal(project.Member.Id, task.CreatedByUserId);
    }

    [Fact]
    public async Task CreateTask_Viewer_IsForbidden()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "vtask");
        using var client = await LoginAsync(factory, project.Viewer);
        var token = await GetSessionTokenAsync(client);

        var response = await client.PostAsync(
            $"/Projects/{project.Id}/Tasks",
            PostData(new Dictionary<string, string?>
            {
                ["Title"] = "No permitida",
                ["Priority"] = "0",
                ["Status"] = "0"
            }, token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateTask_EmptyTitle_IsRejected_NoTaskSaved()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "emptytitle");
        using var client = await LoginAsync(factory, project.Member);
        var token = await GetSessionTokenAsync(client);

        var response = await client.PostAsync(
            $"/Projects/{project.Id}/Tasks",
            PostData(new Dictionary<string, string?>
            {
                ["Title"] = "",
                ["Priority"] = "0",
                ["Status"] = "0"
            }, token));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains($"/Projects/{project.Id}", response.Headers.Location?.ToString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.TaskItems.CountAsync(t => t.ProjectId == project.Id));
    }

    [Fact]
    public async Task CreateTask_OutOfRangeStatus_IsRejected_NoTaskSaved()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "status");
        using var client = await LoginAsync(factory, project.Member);
        var token = await GetSessionTokenAsync(client);

        var response = await client.PostAsync(
            $"/Projects/{project.Id}/Tasks",
            PostData(new Dictionary<string, string?>
            {
                ["Title"] = "Estado forjado",
                ["Priority"] = "0",
                ["Status"] = "99"
            }, token));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.TaskItems.AnyAsync(t => t.Title == "Estado forjado"));
    }

    [Fact]
    public async Task CreateTask_CrossProject_ReturnsNotFound()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var alpha = await SeedProjectAsync(factory, "alpha");
        var beta = await SeedProjectAsync(factory, "beta");
        using var client = await LoginAsync(factory, alpha.Member);
        var token = await GetSessionTokenAsync(client);

        var response = await client.PostAsync(
            $"/Projects/{beta.Id}/Tasks",
            PostData(new Dictionary<string, string?>
            {
                ["Title"] = "Intrusa",
                ["Priority"] = "0",
                ["Status"] = "0"
            }, token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EditTask_PersistsChanges()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "edit");
        using var client = await LoginAsync(factory, project.Manager);
        var token = await GetSessionTokenAsync(client);

        var response = await client.PostAsync(
            $"/Tasks/{project.MembersTask}/Edit",
            PostData(new Dictionary<string, string?>
            {
                ["Title"] = "Tarea editada",
                ["Description"] = "Nueva descripción",
                ["Priority"] = "3",
                ["Status"] = "3",
                ["DueDateUtc"] = "",
                ["AssigneeId"] = project.Viewer.Id
            }, token));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains($"/Tasks/{project.MembersTask}", response.Headers.Location?.ToString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var task = await db.TaskItems.SingleAsync(t => t.Id == project.MembersTask);
        Assert.Equal("Tarea editada", task.Title);
        Assert.Equal("Nueva descripción", task.Description);
        Assert.Equal(TaskStatus.Done, task.Status);
        Assert.Equal(TaskPriority.Urgent, task.Priority);
        Assert.Null(task.DueDateUtc);
        Assert.Equal(project.Viewer.Id, task.AssigneeId);
    }

    [Fact]
    public async Task EditTask_AssigningNonMember_LeavesTaskUnchanged()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "badassign");
        using var client = await LoginAsync(factory, project.Manager);
        var token = await GetSessionTokenAsync(client);

        var response = await client.PostAsync(
            $"/Tasks/{project.MembersTask}/Edit",
            PostData(new Dictionary<string, string?>
            {
                ["Title"] = "Tarea editada",
                ["Description"] = "",
                ["Priority"] = "1",
                ["Status"] = "1",
                ["DueDateUtc"] = "",
                ["AssigneeId"] = project.Outsider.Id
            }, token));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains($"/Tasks/{project.MembersTask}", response.Headers.Location?.ToString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var task = await db.TaskItems.SingleAsync(t => t.Id == project.MembersTask);
        Assert.Equal("Tarea del miembro", task.Title);
        Assert.Equal(project.Member.Id, task.AssigneeId);
    }

    [Fact]
    public async Task DeleteTask_ManagerCan_AndRemovesRelatedData()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "delcomment");
        using var client = await LoginAsync(factory, project.Manager);
        var token = await GetSessionTokenAsync(client);

        using (var seedScope = factory.Services.CreateScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            seedDb.TaskComments.Add(new TaskComment
            {
                Id = Guid.NewGuid(),
                TaskId = project.MembersTask,
                AuthorUserId = project.Member.Id,
                Content = "Comentario a eliminar",
                CreatedAtUtc = DateTime.UtcNow
            });
await seedDb.SaveChangesAsync();
        }

        var response = await client.PostAsync(
            $"/Tasks/{project.MembersTask}/Delete",
            PostData(new Dictionary<string, string?>(), token));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains($"/Projects/{project.Id}", response.Headers.Location?.ToString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.TaskItems.AnyAsync(t => t.Id == project.MembersTask));
        Assert.False(await db.TaskComments.AnyAsync(c => c.TaskId == project.MembersTask));
    }

    [Fact]
    public async Task DeleteTask_MemberAndViewer_AreForbidden()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "nodel");

        using var memberClient = await LoginAsync(factory, project.Member);
        using var viewerClient = await LoginAsync(factory, project.Viewer);
        var memberToken = await GetSessionTokenAsync(memberClient);
        var viewerToken = await GetSessionTokenAsync(viewerClient);

        var memberDelete = await memberClient.PostAsync(
            $"/Tasks/{project.MembersTask}/Delete",
            PostData(new Dictionary<string, string?>(), memberToken));
        var viewerDelete = await viewerClient.PostAsync(
            $"/Tasks/{project.MembersTask}/Delete",
            PostData(new Dictionary<string, string?>(), viewerToken));

        Assert.Equal(HttpStatusCode.Forbidden, memberDelete.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, viewerDelete.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.TaskItems.AnyAsync(t => t.Id == project.MembersTask));
    }

    [Fact]
    public async Task DeleteTask_CrossProject_ReturnsNotFound()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var alpha = await SeedProjectAsync(factory, "alpha");
        var beta = await SeedProjectAsync(factory, "beta");
        using var client = await LoginAsync(factory, alpha.Manager);
        var token = await GetSessionTokenAsync(client);

        var response = await client.PostAsync(
            $"/Tasks/{beta.MembersTask}/Delete",
            PostData(new Dictionary<string, string?>(), token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddComment_MemberCan_ViewerCannot()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "comment");

        using var memberClient = await LoginAsync(factory, project.Member);
        using var viewerClient = await LoginAsync(factory, project.Viewer);
        var memberToken = await GetSessionTokenAsync(memberClient);
        var viewerToken = await GetSessionTokenAsync(viewerClient);

        var allow = await memberClient.PostAsync(
            $"/Tasks/{project.MembersTask}/Comments",
            PostData(new Dictionary<string, string?>
            {
                ["Body"] = "Comentario del miembro"
            }, memberToken));

        Assert.Equal(HttpStatusCode.Redirect, allow.StatusCode);
        Assert.Contains($"/Tasks/{project.MembersTask}", allow.Headers.Location?.ToString());

        var deny = await viewerClient.PostAsync(
            $"/Tasks/{project.MembersTask}/Comments",
            PostData(new Dictionary<string, string?>
            {
                ["Body"] = "No permitido"
            }, viewerToken));

        Assert.Equal(HttpStatusCode.Forbidden, deny.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var comments = await db.TaskComments.Where(c => c.TaskId == project.MembersTask).ToListAsync();
        Assert.Single(comments);
        Assert.Equal("Comentario del miembro", comments[0].Content);
        Assert.Equal(project.Member.Id, comments[0].AuthorUserId);
    }

    [Fact]
    public async Task AddComment_EmptyBody_IsRejected_NoCommentSaved()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "emptycom");
        using var client = await LoginAsync(factory, project.Member);
        var token = await GetSessionTokenAsync(client);

        var response = await client.PostAsync(
            $"/Tasks/{project.MembersTask}/Comments",
            PostData(new Dictionary<string, string?>
            {
                ["Body"] = "   "
            }, token));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.TaskComments.Where(c => c.TaskId == project.MembersTask).ToListAsync());
    }

    [Fact]
    public async Task AddComment_CrossProject_ReturnsNotFound()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var alpha = await SeedProjectAsync(factory, "alpha");
        var beta = await SeedProjectAsync(factory, "beta");
        using var client = await LoginAsync(factory, alpha.Member);
        var token = await GetSessionTokenAsync(client);

        var response = await client.PostAsync(
            $"/Tasks/{beta.MembersTask}/Comments",
            PostData(new Dictionary<string, string?>
            {
                ["Body"] = "Intruso"
            }, token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateKey_SecondCreate_ShowsError_NoExtraProject()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var owner = await CreateUserAsync(factory, "dupe@example.com");
        using var client = await LoginAsync(factory, owner);
        var token = await GetSessionTokenAsync(client);

        var first = await client.PostAsync("/Projects", PostData(new Dictionary<string, string?>
        {
            ["Name"] = "Primero",
            ["Key"] = "DUP",
            ["Description"] = ""
        }, token));
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);

        var second = await client.PostAsync("/Projects", PostData(new Dictionary<string, string?>
        {
            ["Name"] = "Segundo",
            ["Key"] = "dup",
            ["Description"] = ""
        }, token));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var html = await second.Content.ReadAsStringAsync();
        Assert.Contains("Ya existe un proyecto con esa clave", WebUtility.HtmlDecode(html));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.Projects.Where(p => p.Key == "DUP").Where(p => p.Name == "Segundo").ToListAsync());
        Assert.Equal(1, await db.Projects.CountAsync(p => p.Key == "DUP"));
    }

    [Fact]
    public async Task Admin_WithoutMembership_ProjectAndTask_ReturnNotFound()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var project = await SeedProjectAsync(factory, "admin");
        using var client = await LoginAsync(factory, project.Owner);
        var token = await GetSessionTokenAsync(client);

        var adminProject = await client.PostAsync("/Projects", PostData(new Dictionary<string, string?>
        {
            ["Name"] = "Admin real",
            ["Key"] = "ADMIN1",
            ["Description"] = ""
        }, token));
        Assert.Equal(HttpStatusCode.Redirect, adminProject.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var adminOwned = await db.Projects.SingleAsync(p => p.Key == "ADMIN1");

        using var adminClient = await LoginAsync(factory, await CreateUserAsync(factory, "admin2@taskflow.local"));
        using (var roleScope = factory.Services.CreateScope())
        {
            var um = roleScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var target = await um.FindByEmailAsync("admin2@taskflow.local");
            Assert.NotNull(target);
            await um.AddToRoleAsync(target!, "Admin");
        }

        var projectResponse = await adminClient.GetAsync($"/Projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NotFound, projectResponse.StatusCode);

        var taskResponse = await adminClient.GetAsync($"/Tasks/{project.MembersTask}");
        Assert.Equal(HttpStatusCode.NotFound, taskResponse.StatusCode);
    }

    [Fact]
    public async Task InvalidProjectGuid_Route_ReturnsNotFound()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        var owner = await CreateUserAsync(factory, "badguid@example.com");
        using var client = await LoginAsync(factory, owner);

        var response = await client.GetAsync("/Projects/no-es-un-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}