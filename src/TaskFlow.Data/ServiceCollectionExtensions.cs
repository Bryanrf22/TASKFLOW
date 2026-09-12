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
        var connectionString = configuration.GetConnectionString("Default");
        var isSqlServer = providerName.Equals(nameof(DatabaseProvider.SqlServer), StringComparison.OrdinalIgnoreCase);

        if (isSqlServer && string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database:Provider=SqlServer requiere una cadena de conexión. Define ConnectionStrings:Default (por ejemplo, la variable de entorno TF_CONNECTION_STRING).");
        }

        connectionString ??= "Data Source=taskflow.db";

        services.AddScoped<IProjectMembershipReader, ProjectMembershipReader>();

        services.AddDbContext<AppDbContext>(options =>
        {
            if (isSqlServer)
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