using Microsoft.EntityFrameworkCore;
using TaskFlow.Core.Domain.Abstractions;
using TaskFlow.Core.Domain.Entities;
using TaskFlow.Core.Domain.Enums;
using TaskFlow.Core.Domain.Rules;
using TaskFlow.Data;
using TaskFlow.Web.Models.Projects;
using TaskFlow.Web.Models.Tasks;

namespace TaskFlow.Web.Services;

public sealed class TaskService
{
    private readonly AppDbContext _db;
    private readonly IProjectMembershipReader _memberships;

    public TaskService(AppDbContext db, IProjectMembershipReader memberships)
    {
        _db = db;
        _memberships = memberships;
    }

    public async Task<OperationResult<Guid>> CreateAsync(string userId, Guid projectId, CreateTaskViewModel model)
    {
        var role = await _memberships.GetRoleAsync(userId, projectId);
        if (role is null)
            return OperationResult<Guid>.NotFound();
        if (!Permissions.CanCreateTask(role.Value))
            return OperationResult<Guid>.Forbidden();

        var assigneeId = string.IsNullOrWhiteSpace(model.AssigneeId) ? null : model.AssigneeId;
        if (assigneeId is not null
            && !await _db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == assigneeId))
            return OperationResult<Guid>.Invalid();

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = model.Title.Trim(),
            Description = model.Description?.Trim() ?? string.Empty,
            Status = model.Status,
            Priority = model.Priority,
            DueDateUtc = model.DueDateUtc,
            AssigneeId = assigneeId,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.TaskItems.Add(task);
        await _db.SaveChangesAsync();
        return OperationResult<Guid>.Ok(task.Id);
    }

    public async Task<OperationResult> UpdateAsync(string userId, Guid taskId, EditTaskViewModel model)
    {
        var task = await _db.TaskItems.SingleOrDefaultAsync(t => t.Id == taskId);
        if (task is null)
            return OperationResult.NotFound();

        var role = await _memberships.GetRoleAsync(userId, task.ProjectId);
        if (role is null)
            return OperationResult.NotFound();

        var ownsTask = task.AssigneeId == userId || task.CreatedByUserId == userId;
        if (!Permissions.CanEditTask(role.Value, ownsTask))
            return OperationResult.Forbidden();

        var assigneeId = string.IsNullOrWhiteSpace(model.AssigneeId) ? null : model.AssigneeId;
        if (assigneeId is not null
            && !await _db.ProjectMembers.AnyAsync(m => m.ProjectId == task.ProjectId && m.UserId == assigneeId))
            return OperationResult.Invalid();

        task.Title = model.Title.Trim();
        task.Description = model.Description?.Trim() ?? string.Empty;
        task.Status = model.Status;
        task.Priority = model.Priority;
        task.DueDateUtc = model.DueDateUtc;
        task.AssigneeId = assigneeId;
        task.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    public async Task<OperationResult<Guid>> DeleteAsync(string userId, Guid taskId)
    {
        var task = await _db.TaskItems.AsNoTracking().SingleOrDefaultAsync(t => t.Id == taskId);
        if (task is null)
            return OperationResult<Guid>.NotFound();

        var role = await _memberships.GetRoleAsync(userId, task.ProjectId);
        if (role is null)
            return OperationResult<Guid>.NotFound();
        if (!Permissions.CanDeleteTask(role.Value))
            return OperationResult<Guid>.Forbidden();

        var projectId = task.ProjectId;

        await _db.TaskComments.Where(c => c.TaskId == taskId).ExecuteDeleteAsync();
        await _db.TaskHistoryEntries.Where(h => h.TaskId == taskId).ExecuteDeleteAsync();
        await _db.TaskLabels.Where(tl => tl.TaskId == taskId).ExecuteDeleteAsync();
        await _db.Notifications.Where(n => n.TaskId == taskId).ExecuteDeleteAsync();
        await _db.TaskItems.Where(t => t.Id == taskId).ExecuteDeleteAsync();

        return OperationResult<Guid>.Ok(projectId);
    }

    public async Task<OperationResult<TaskDetailsViewModel>> GetDetailsAsync(string userId, Guid taskId)
    {
        var task = await _db.TaskItems.AsNoTracking().SingleOrDefaultAsync(t => t.Id == taskId);
        if (task is null)
            return OperationResult<TaskDetailsViewModel>.NotFound();

        var role = await _memberships.GetRoleAsync(userId, task.ProjectId);
        if (role is null)
            return OperationResult<TaskDetailsViewModel>.NotFound();

        var ownsTask = task.AssigneeId == userId || task.CreatedByUserId == userId;
        var project = await _db.Projects.AsNoTracking().SingleOrDefaultAsync(p => p.Id == task.ProjectId);

        var comments = await (from c in _db.TaskComments
                              where c.TaskId == taskId
                              join u in _db.Users on c.AuthorUserId equals u.Id
                              orderby c.CreatedAtUtc
                              select new TaskCommentViewModel
                              {
                                  Id = c.Id,
                                  Body = c.Content,
                                  AuthorName = u.DisplayName,
                                  CreatedAtUtc = c.CreatedAtUtc
                              })
            .ToListAsync();

        var assignables = await (from m in _db.ProjectMembers
                                 where m.ProjectId == task.ProjectId
                                 join u in _db.Users on m.UserId equals u.Id
                                 orderby u.Email
                                 select new MemberViewModel
                                 {
                                     UserId = m.UserId,
                                     Email = u.Email ?? string.Empty,
                                     DisplayName = u.DisplayName,
                                     Role = m.Role,
                                     JoinedAtUtc = m.JoinedAtUtc
                                 })
            .ToListAsync();

        var details = new TaskDetailsViewModel
        {
            Task = task,
            ProjectId = task.ProjectId,
            ProjectName = project?.Name ?? string.Empty,
            MyRole = role.Value,
            OwnsTask = ownsTask,
            CanEdit = Permissions.CanEditTask(role.Value, ownsTask),
            CanDelete = Permissions.CanDeleteTask(role.Value),
            CanComment = Permissions.CanComment(role.Value),
            EditModel = new EditTaskViewModel
            {
                Title = task.Title,
                Description = task.Description,
                Priority = task.Priority,
                Status = task.Status,
                DueDateUtc = task.DueDateUtc,
                AssigneeId = task.AssigneeId,
                AssignableMembers = assignables
            },
            Comments = comments,
            AssignableMembers = assignables
        };

        return OperationResult<TaskDetailsViewModel>.Ok(details);
    }

    public async Task<OperationResult> AddCommentAsync(string userId, Guid taskId, AddTaskCommentViewModel model)
    {
        var task = await _db.TaskItems.AsNoTracking().SingleOrDefaultAsync(t => t.Id == taskId);
        if (task is null)
            return OperationResult.NotFound();

        var role = await _memberships.GetRoleAsync(userId, task.ProjectId);
        if (role is null)
            return OperationResult.NotFound();
        if (!Permissions.CanComment(role.Value))
            return OperationResult.Forbidden();

        _db.TaskComments.Add(new TaskComment
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            AuthorUserId = userId,
            Content = model.Body.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return OperationResult.Ok();
    }
}