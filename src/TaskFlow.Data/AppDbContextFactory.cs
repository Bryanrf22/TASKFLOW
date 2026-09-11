using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskFlow.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var providerName = Environment.GetEnvironmentVariable("TF_DATABASE_PROVIDER") ?? nameof(DatabaseProvider.Sqlite);
        var connectionString = Environment.GetEnvironmentVariable("TF_CONNECTION_STRING");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        if (providerName.Equals(nameof(DatabaseProvider.SqlServer), StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseSqlServer(
                connectionString ?? "Server=localhost;Database=TaskFlow;Trusted_Connection=True;TrustServerCertificate=True");
        }
        else
        {
            optionsBuilder.UseSqlite(connectionString ?? "Data Source=taskflow-dev.db");
        }

        return new AppDbContext(optionsBuilder.Options);
    }
}