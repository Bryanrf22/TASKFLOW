using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Data;

namespace TaskFlow.IntegrationTests;

public class AuthFlowTests
{
    private const string ValidPassword = "Clave#12345";

    private static readonly Regex AntiforgeryTokenRegex = new(
        @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""",
        RegexOptions.Compiled);

    private static string NewDbPath()
        => $"/tmp/taskflow-auth-{Guid.NewGuid():N}.db";

    private static WebApplicationFactory<Program> CreateFactory(
        string dbPath,
        Action<IWebHostBuilder>? configure = null)
        => new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:Default", $"Data Source={dbPath}");
                configure?.Invoke(builder);
            });

    private static async Task<AppUser> CreateUserAsync(
        WebApplicationFactory<Program> factory,
        string email,
        string password = ValidPassword)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            DisplayName = "Usuario de prueba",
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        return user;
    }

    private static async Task<string> GetFormTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = AntiforgeryTokenRegex.Match(html);
        Assert.True(match.Success, $"Token antiforgery no encontrado en {url}.");
        return match.Groups[1].Value;
    }

    private static async Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        var token = await GetFormTokenAsync(client, "/Account/Login");
        var content = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["RememberMe"] = "false",
            ["ReturnUrl"] = "",
            ["__RequestVerificationToken"] = token
        });

        return await client.PostAsync("/Account/Login", content);
    }

    [Fact]
    public async Task UnauthenticatedRequest_ToAdminArea_RedirectsToLogin()
    {
        await using var factory = CreateFactory(NewDbPath());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Users");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Register_ValidUser_RedirectsToConfirmation_AndCannotLoginYet()
    {
        await using var factory = CreateFactory(NewDbPath());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var token = await GetFormTokenAsync(client, "/Account/Register");
        var content = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["DisplayName"] = "Ana García",
            ["Email"] = "ana.garcia@example.com",
            ["Password"] = ValidPassword,
            ["ConfirmPassword"] = ValidPassword,
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Account/Register", content);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/RegisterConfirmation", response.Headers.Location?.ToString());

        var afterRegister = await client.GetAsync("/Projects");
        Assert.Equal(HttpStatusCode.Redirect, afterRegister.StatusCode);
        Assert.Contains("/Account/Login", afterRegister.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Register_WithoutAntiforgeryToken_ReturnsBadRequest()
    {
        await using var factory = CreateFactory(NewDbPath());
        using var client = factory.CreateClient();

        var content = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["DisplayName"] = "Ana García",
            ["Email"] = "ana.garcia@example.com",
            ["Password"] = ValidPassword,
            ["ConfirmPassword"] = ValidPassword
        });

        var response = await client.PostAsync("/Account/Register", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Fails_AndKeepsAdminAreaLocked()
    {
        await using var factory = CreateFactory(NewDbPath());
        await CreateUserAsync(factory, "sara@example.com");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var login = await PostLoginAsync(client, "sara@example.com", "Contraseña#Incorrecta");
        var loginHtml = await login.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains("Email o contraseña incorrectos", WebUtility.HtmlDecode(loginHtml));

        var admin = await client.GetAsync("/Users");
        Assert.Equal(HttpStatusCode.Redirect, admin.StatusCode);
    }

    [Fact]
    public async Task Login_CorrectPassword_Succeeds_ButAdminAreaReturnsAccessDenied()
    {
        await using var factory = CreateFactory(NewDbPath());
        await CreateUserAsync(factory, "sara@example.com");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var login = await PostLoginAsync(client, "sara@example.com", ValidPassword);

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/", login.Headers.Location?.ToString());

        var admin = await client.GetAsync("/Users");
        Assert.Equal(HttpStatusCode.Redirect, admin.StatusCode);
        Assert.Contains("/Account/AccessDenied", admin.Headers.Location?.ToString());
    }

    [Fact]
    public async Task SeededAdmin_CanLogin_AndAccessAdminArea()
    {
        await using var factory = CreateFactory(NewDbPath());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var login = await PostLoginAsync(client, "admin@taskflow.local", "Taskflow#Admin2026");

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var admin = await client.GetAsync("/Users");
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
    }

    [Fact]
    public async Task Lockout_PreventsLoginEvenWithCorrectPassword()
    {
        var dbPath = NewDbPath();
        await using var factory = CreateFactory(dbPath, builder =>
            builder.ConfigureServices(services =>
                services.Configure<IdentityOptions>(options =>
                {
                    options.Lockout.MaxFailedAccessAttempts = 3;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
                })));

        await CreateUserAsync(factory, "lockout@example.com");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var failed = await PostLoginAsync(client, "lockout@example.com", "Contraseña#Incorrecta");
            Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        }

        var locked = await PostLoginAsync(client, "lockout@example.com", ValidPassword);
        var lockedHtml = await locked.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
        Assert.Contains("bloqueada", lockedHtml);

        var admin = await client.GetAsync("/Users");
        Assert.Equal(HttpStatusCode.Redirect, admin.StatusCode);
    }
}