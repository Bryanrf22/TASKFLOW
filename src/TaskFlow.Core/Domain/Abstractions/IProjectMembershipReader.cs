using TaskFlow.Core.Domain.Enums;

namespace TaskFlow.Core.Domain.Abstractions;

public interface IProjectMembershipReader
{
    Task<ProjectRole?> GetRoleAsync(string userId, Guid projectId, CancellationToken cancellationToken = default);
}