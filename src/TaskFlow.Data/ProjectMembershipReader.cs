using Microsoft.EntityFrameworkCore;
using TaskFlow.Core.Domain.Abstractions;
using TaskFlow.Core.Domain.Enums;

namespace TaskFlow.Data;

public sealed class ProjectMembershipReader : IProjectMembershipReader
{
    private readonly AppDbContext _db;

    public ProjectMembershipReader(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ProjectRole?> GetRoleAsync(string userId, Guid projectId, CancellationToken cancellationToken = default)
        => await _db.ProjectMembers
            .Where(m => m.UserId == userId && m.ProjectId == projectId)
            .Select(m => (ProjectRole?)m.Role)
            .SingleOrDefaultAsync(cancellationToken);
}