using Microsoft.Data.SqlClient;
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

        services.AddScoped<IProjectMembershipReader, ProjectMembershipReader>();

        services.AddDbContext<AppDbContext>(options =>
        {
            if (isSqlServer)
            {
                connectionString = Environment.GetEnvironmentVariable("TF_CONNECTION_STRING") ?? connectionString;
                ValidateSqlServerConnectionString(connectionString);
                options.UseSqlServer(connectionString);
            }
            else
            {
                options.UseSqlite(connectionString ?? "Data Source=taskflow.db");
            }
        });

        return services;
    }

    private static void ValidateSqlServerConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database:Provider=SqlServer requiere una cadena de conexión. Define ConnectionStrings:Default o la variable de entorno TF_CONNECTION_STRING.");
        }

        try
        {
            _ = new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "La cadena de conexión configurada no es válida para SqlServer (¿heredada del proveedor SQLite, por ejemplo \"Data Source=taskflow.db;Cache=Shared\"?). Configura una cadena SqlServer real en ConnectionStrings:Default o TF_CONNECTION_STRING.",
                exception);
        }
    }
}