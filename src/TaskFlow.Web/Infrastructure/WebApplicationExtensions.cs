using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;

namespace TaskFlow.Web.Infrastructure;

public static class WebApplicationExtensions
{
    public static void MigrateDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();

        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        seeder.SeedAsync().GetAwaiter().GetResult();
    }
}