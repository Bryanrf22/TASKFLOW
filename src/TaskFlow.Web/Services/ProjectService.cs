using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Core.Domain.Abstractions;
using TaskFlow.Core.Domain.Entities;
using TaskFlow.Core.Domain.Enums;
using TaskFlow.Core.Domain.Rules;
using TaskFlow.Data;
using TaskFlow.Web.Models.Projects;
using TaskFlow.Web.Models.Tasks;

namespace TaskFlow.Web.Services;

public sealed class ProjectService
{
    private readonly AppDbContext _db;
    private readonly IProjectMembershipReader _memberships;
    private readonly UserManager<AppUser> _userManager;

    public ProjectService(
        AppDbContext db,
        IProjectMembershipReader memberships,
        UserManager<AppUser> userManager)
    {
        _db = db;
        _memberships = memberships;
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<ProjectListItemViewModel>> ListForUserAsync(string userId)
        => await _db.ProjectMembers
            .Where(m => m.UserId == userId)
            .Join(
                _db.Projects,
                m => m.ProjectId,
                p => p.Id,
                (m, p) => new ProjectListItemViewModel
                {
                    ProjectId = p.Id,
                    Name = p.Name,
                    Key = p.Key,
                    MyRole = m.Role,
                    CreatedAtUtc = p.CreatedAtUtc
                })
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync();

    public async Task<OperationResult<Guid>> CreateAsync(string userId, CreateProjectViewModel model)
    {
        var key = model.Key.Trim().ToUpperInvariant();

        if (await _db.Projects.AnyAsync(p => p.Key == key))
            return OperationResult<Guid>.Conflict();

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = model.Name.Trim(),
            Key = key,
            Description = model.Description?.Trim() ?? string.Empty,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Projects.Add(project);
        _db.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = userId,
            Role = ProjectRole.Owner,
            JoinedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return OperationResult<Guid>.Ok(project.Id);
    }

    public async Task<OperationResult<ProjectDetailsViewModel>> GetDetailsAsync(string userId, Guid projectId)
    {
        var project = await _db.Projects.AsNoTracking().SingleOrDefaultAsync(p => p.Id == projectId);
        if (project is null)
            return OperationResult<ProjectDetailsViewModel>.NotFound();

        var role = await _memberships.GetRoleAsync(userId, projectId);
        if (role is null)
            return OperationResult<ProjectDetailsViewModel>.NotFound();

        var members = await ListMembersAsync(projectId);
        var tasks = await ListTaskRowsAsync(projectId);

        var details = new ProjectDetailsViewModel
        {
            Project = project,
            MyRole = role.Value,
            CanEditSettings = Permissions.CanManageProjectSettings(role.Value),
            CanDelete = Permissions.CanDeleteProject(role.Value),
            CanManageMembers = Permissions.CanManageMembers(role.Value),
            CanCreateTask = Permissions.CanCreateTask(role.Value),
            Members = members,
            AssignableMembers = members,
            Tasks = tasks,
            CreateMember = new CreateMemberViewModel(),
            CreateTask = new CreateTaskViewModel { AssignableMembers = members },
            EditModel = new EditProjectViewModel { Name = project.Name, Description = project.Description }
        };

        return OperationResult<ProjectDetailsViewModel>.Ok(details);
    }

    public async Task<OperationResult> UpdateSettingsAsync(string userId, Guid projectId, EditProjectViewModel model)
    {
        var role = await _memberships.GetRoleAsync(userId, projectId);
        if (role is null)
            return OperationResult.NotFound();
        if (!Permissions.CanManageProjectSettings(role.Value))
            return OperationResult.Forbidden();

        var project = await _db.Projects.SingleAsync(p => p.Id == projectId);
        project.Name = model.Name.Trim();
        project.Description = model.Description?.Trim() ?? string.Empty;
        await _db.SaveChangesAsync();

        return OperationResult.Ok();
    }

    public async Task<OperationResult> DeleteAsync(string userId, Guid projectId)
    {
        var role = await _memberships.GetRoleAsync(userId, projectId);
        if (role is null)
            return OperationResult.NotFound();
        if (!Permissions.CanDeleteProject(role.Value))
            return OperationResult.Forbidden();

        await DeleteProjectDataAsync(projectId);
        return OperationResult.Ok();
    }

    public async Task<OperationResult> AddMemberAsync(string actorUserId, Guid projectId, CreateMemberViewModel model)
    {
        var role = await _memberships.GetRoleAsync(actorUserId, projectId);
        if (role is null)
            return OperationResult.NotFound();
        if (!Permissions.CanManageMembers(role.Value))
            return OperationResult.Forbidden();
        if (!Enum.IsDefined(typeof(ProjectRole), model.Role))
            return OperationResult.Invalid();
        if (model.Role == ProjectRole.Owner && !Permissions.CanGrantOwner(role.Value))
            return OperationResult.Forbidden();

        var target = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (target is null)
            return OperationResult.Invalid();

        if (await _db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == target.Id))
            return OperationResult.Conflict();

        _db.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = target.Id,
            Role = model.Role,
            JoinedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    public async Task<OperationResult> ChangeMemberRoleAsync(string actorUserId, Guid projectId, string targetUserId, ProjectRole newRole)
    {
        var role = await _memberships.GetRoleAsync(actorUserId, projectId);
        if (role is null)
            return OperationResult.NotFound();
        if (!Permissions.CanManageMembers(role.Value))
            return OperationResult.Forbidden();
        if (!Enum.IsDefined(typeof(ProjectRole), newRole))
            return OperationResult.Invalid();

        var target = await _db.ProjectMembers.SingleOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == targetUserId);
        if (target is null)
            return OperationResult.NotFound();

        if (!Permissions.CanModifyMember(role.Value, target.Role))
            return OperationResult.Forbidden();
        if (newRole == ProjectRole.Owner && !Permissions.CanGrantOwner(role.Value))
            return OperationResult.Forbidden();

        if (target.Role == ProjectRole.Owner && newRole != ProjectRole.Owner)
        {
            using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var ownerCount = await _db.ProjectMembers.CountAsync(
                m => m.ProjectId == projectId && m.Role == ProjectRole.Owner);
            if (ownerCount <= 1)
            {
                await transaction.RollbackAsync();
                return OperationResult.Conflict();
            }

            target.Role = newRole;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return OperationResult.Ok();
        }

        target.Role = newRole;
        await _db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    public async Task<OperationResult> RemoveMemberAsync(string actorUserId, Guid projectId, string targetUserId)
    {
        var role = await _memberships.GetRoleAsync(actorUserId, projectId);
        if (role is null)
            return OperationResult.NotFound();
        if (!Permissions.CanManageMembers(role.Value))
            return OperationResult.Forbidden();

        var target = await _db.ProjectMembers.SingleOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == targetUserId);
        if (target is null)
            return OperationResult.NotFound();

        if (target.Role == ProjectRole.Owner)
        {
            if (!Permissions.CanGrantOwner(role.Value))
                return OperationResult.Forbidden();

            using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var ownerCount = await _db.ProjectMembers.CountAsync(
                m => m.ProjectId == projectId && m.Role == ProjectRole.Owner);
            if (ownerCount <= 1)
            {
                await transaction.RollbackAsync();
                return OperationResult.Conflict();
            }

            await RemoveMemberAsync(projectId, targetUserId);
            await transaction.CommitAsync();
            return OperationResult.Ok();
        }

        await RemoveMemberAsync(projectId, targetUserId);
        return OperationResult.Ok();
    }

    private async Task RemoveMemberAsync(Guid projectId, string targetUserId)
    {
        await _db.TaskItems
            .Where(t => t.ProjectId == projectId && t.AssigneeId == targetUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.AssigneeId, (string?)null));

        var membership = await _db.ProjectMembers.SingleAsync(m => m.ProjectId == projectId && m.UserId == targetUserId);
        _db.ProjectMembers.Remove(membership);
        await _db.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<MemberViewModel>> ListMembersAsync(Guid projectId)
        => await (from m in _db.ProjectMembers
                  where m.ProjectId == projectId
                  join u in _db.Users on m.UserId equals u.Id
                  orderby m.Role descending, u.Email
                  select new MemberViewModel
                  {
                      UserId = m.UserId,
                      Email = u.Email ?? string.Empty,
                      DisplayName = u.DisplayName,
                      Role = m.Role,
                      JoinedAtUtc = m.JoinedAtUtc
                  })
            .ToListAsync();

    private async Task<IReadOnlyList<TaskListItemViewModel>> ListTaskRowsAsync(Guid projectId)
        => await (from t in _db.TaskItems
                  where t.ProjectId == projectId
                  join a in _db.Users on t.AssigneeId equals a.Id into g
                  from assignee in g.DefaultIfEmpty()
                  orderby t.CreatedAtUtc descending
                  select new TaskListItemViewModel
                  {
                      Id = t.Id,
                      ProjectId = t.ProjectId,
                      Title = t.Title,
                      Status = t.Status,
                      Priority = t.Priority,
                      DueDateUtc = t.DueDateUtc,
                      AssigneeEmail = assignee == null ? null : assignee.Email
                  })
            .ToListAsync();

    private async Task DeleteProjectDataAsync(Guid projectId)
    {
        var taskIds = await _db.TaskItems.Where(t => t.ProjectId == projectId).Select(t => t.Id).ToListAsync();

        await using var transaction = await _db.Database.BeginTransactionAsync();
        await _db.TaskComments.Where(c => taskIds.Contains(c.TaskId)).ExecuteDeleteAsync();
        await _db.TaskItems.Where(t => t.ProjectId == projectId).ExecuteDeleteAsync();
        await _db.ProjectMembers.Where(m => m.ProjectId == projectId).ExecuteDeleteAsync();
        await _db.Projects.Where(p => p.Id == projectId).ExecuteDeleteAsync();
        await transaction.CommitAsync();
    }
}