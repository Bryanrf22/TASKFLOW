using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Core.Domain.Authorization;
using TaskFlow.Data;

namespace TaskFlow.IntegrationTests;

public class AdminAndAccountTests
{
    private const string ValidPassword = "Clave#12345";

    private static readonly Regex AntiforgeryTokenRegex = new(
        @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""",
        RegexOptions.Compiled);

    private static string NewDbPath()
        => $"/tmp/taskflow-admin-{Guid.NewGuid():N}.db";

    private static WebApplicationFactory<Program> CreateFactory(
        string dbPath,
        Action<IWebHostBuilder>? configure = null)
        => new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:Default", $"Data Source={dbPath}");
                configure?.Invoke(builder);
            });

    private static WebApplicationFactory<Program> CreateSeededFactory(string dbPath)
        => CreateFactory(dbPath, builder =>
            builder.UseSetting("Seed:AdminEmail", "admin@taskflow.local")
                   .UseSetting("Seed:AdminPassword", "Taskflow#Admin2026"));

    private static async Task<AppUser> CreateUserAsync(
        WebApplicationFactory<Program> factory,
        string email)
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

        var result = await userManager.CreateAsync(user, ValidPassword);
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
        string password,
        string returnUrl = "")
    {
        var token = await GetFormTokenAsync(client, "/Account/Login");
        var content = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["RememberMe"] = "false",
            ["ReturnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = token
        });

        return await client.PostAsync("/Account/Login", content);
    }

    [Fact]
    public async Task SeededAdmin_PromoteUserToAdmin_Succeeds()
    {
        var factory = CreateSeededFactory(NewDbPath());
        await using var _ = factory;
        var target = await CreateUserAsync(factory, "promote@example.com");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var login = await PostLoginAsync(client, "admin@taskflow.local", "Taskflow#Admin2026");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var page = await client.GetStringAsync("/Users");
        var token = AntiforgeryTokenRegex.Match(page).Groups[1].Value;

        var response = await client.PostAsync(
            "/Users/ToggleAdmin",
            new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["userId"] = target.Id,
                ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Users", response.Headers.Location?.ToString());

        using (var verifyScope = factory.Services.CreateScope())
        {
            var verifyManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            Assert.True(await verifyManager.IsInRoleAsync(target, ApplicationRoles.Admin));
        }
    }

    [Fact]
    public async Task SeededAdmin_DemoteOtherAdmin_Succeeds()
    {
        var factory = CreateSeededFactory(NewDbPath());
        await using var _ = factory;
        var target = await CreateUserAsync(factory, "demote@example.com");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using (var promoScope = factory.Services.CreateScope())
        {
            var userManager = promoScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var tracked = await userManager.FindByIdAsync(target.Id);
            Assert.NotNull(tracked);
            await userManager.AddToRoleAsync(tracked!, ApplicationRoles.Admin);
        }

        var login = await PostLoginAsync(client, "admin@taskflow.local", "Taskflow#Admin2026");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var page = await client.GetStringAsync("/Users");
        var token = AntiforgeryTokenRegex.Match(page).Groups[1].Value;

        var response = await client.PostAsync(
            "/Users/ToggleAdmin",
            new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["userId"] = target.Id,
                ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using (var verifyScope = factory.Services.CreateScope())
        {
            var verifyManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            Assert.False(await verifyManager.IsInRoleAsync(target, ApplicationRoles.Admin));
        }
    }

    [Fact]
    public async Task Admin_SelfDemote_IsRejected_AndKeepsRole()
    {
        var factory = CreateSeededFactory(NewDbPath());
        await using var _ = factory;
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var login = await PostLoginAsync(client, "admin@taskflow.local", "Taskflow#Admin2026");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        AppUser admin;
        using (var lookupScope = factory.Services.CreateScope())
        {
            var lookupManager = lookupScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            admin = (await lookupManager.FindByEmailAsync("admin@taskflow.local"))!;
        }

        var page = await client.GetStringAsync("/Users");
        var token = AntiforgeryTokenRegex.Match(page).Groups[1].Value;

        var response = await client.PostAsync(
            "/Users/ToggleAdmin",
            new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["userId"] = admin.Id,
                ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using (var verifyScope = factory.Services.CreateScope())
        {
            var verifyManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var persisted = await verifyManager.FindByEmailAsync("admin@taskflow.local");
            Assert.NotNull(persisted);
            Assert.True(await verifyManager.IsInRoleAsync(persisted!, ApplicationRoles.Admin));
        }
    }

    [Fact]
    public async Task ToggleAdmin_UnknownUser_ReturnsNotFound()
    {
        var factory = CreateSeededFactory(NewDbPath());
        await using var _ = factory;
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var login = await PostLoginAsync(client, "admin@taskflow.local", "Taskflow#Admin2026");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var page = await client.GetStringAsync("/Users");
        var token = AntiforgeryTokenRegex.Match(page).Groups[1].Value;

        var response = await client.PostAsync(
            "/Users/ToggleAdmin",
            new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["userId"] = Guid.NewGuid().ToString(),
                ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ToggleAdmin_EmptyUserId_ReturnsBadRequest()
    {
        var factory = CreateSeededFactory(NewDbPath());
        await using var _ = factory;
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var login = await PostLoginAsync(client, "admin@taskflow.local", "Taskflow#Admin2026");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var page = await client.GetStringAsync("/Users");
        var token = AntiforgeryTokenRegex.Match(page).Groups[1].Value;

        var response = await client.PostAsync(
            "/Users/ToggleAdmin",
            new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["userId"] = "   ",
                ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsSession_AndProtectedAreaRedirectsToLogin()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        await CreateUserAsync(factory, "logout@example.com");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var login = await PostLoginAsync(client, "logout@example.com", ValidPassword);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var page = await client.GetStringAsync("/Projects");
        var token = AntiforgeryTokenRegex.Match(page).Groups[1].Value;

        var logout = await client.PostAsync(
            "/Account/Logout",
            new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("/", logout.Headers.Location?.ToString());

        var after = await client.GetAsync("/Users");
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.Contains("/Account/Login", after.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Register_DuplicateEmail_DoesNotRevealExistingAccount()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        await CreateUserAsync(factory, "duplicado@example.com");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var token = await GetFormTokenAsync(client, "/Account/Register");
        var response = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["DisplayName"] = "Nadia",
            ["Email"] = "duplicado@example.com",
            ["Password"] = ValidPassword,
            ["ConfirmPassword"] = ValidPassword,
            ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/RegisterConfirmation", response.Headers.Location?.ToString());

        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Ya existe una cuenta", html);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        Assert.Equal(1, userManager.Users.Count(u => u.Email == "duplicado@example.com"));
    }

    [Fact]
    public async Task Login_ExternalReturnUrl_RedirectsHome_NotToExternalSite()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        await CreateUserAsync(factory, "redirect@example.com");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var login = await PostLoginAsync(client, "redirect@example.com", ValidPassword, returnUrl: "https://evil.example.com");

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/", login.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Login_LocalReturnUrl_IsPreserved()
    {
        var factory = CreateFactory(NewDbPath());
        await using var _ = factory;
        await CreateUserAsync(factory, "redirect@example.com");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var login = await PostLoginAsync(client, "redirect@example.com", ValidPassword, returnUrl: "/Projects");

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/Projects", login.Headers.Location?.ToString());
    }
}