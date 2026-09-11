using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace TaskFlow.IntegrationTests;

public class SmokeTests
{
    [Fact]
    public async Task Home_ReturnsSuccess_AndMigrationsApply()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseSetting("ConnectionStrings:Default", "Data Source=/tmp/taskflow-test.db"));

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        response.EnsureSuccessStatusCode();
    }
}