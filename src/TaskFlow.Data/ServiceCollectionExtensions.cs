using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Core.Domain.Abstractions;

namespace TaskFlow.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var providerName = configuration["Database:Provider"] ?? nameof(DatabaseProvider.Sqlite);
        var connectionString = configuration.GetConnectionString("Default") ?? "Data Source=taskflow.db";

        services.AddScoped<IProjectMembershipReader, ProjectMembershipReader>();

        services.AddDbContext<AppDbContext>(options =>
        {
            if (providerName.Equals(nameof(DatabaseProvider.SqlServer), StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(connectionString);
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        });

        return services;
    }
}