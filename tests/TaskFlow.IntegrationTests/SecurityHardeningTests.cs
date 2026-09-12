using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Data;

namespace TaskFlow.IntegrationTests;

public class SecurityHardeningTests
{
    private const string ValidPassword = "Clave#12345";

    private static readonly Regex AntiforgeryTokenRegex = new(
        @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex ConfirmationLinkRegex = new(
        @"href=""([^""]*/Account/ConfirmEmail\?[^""]+)""",
        RegexOptions.Compiled);

    private static string NewDbPath()
        => $"/tmp/taskflow-security-{Guid.NewGuid():N}.db";

    private static WebApplicationFactory<Program> CreateFactory(string dbPath)
        => new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseSetting("ConnectionStrings:Default", $"Data Source={dbPath}"));

    private static async Task<string> GetFormTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = AntiforgeryTokenRegex.Match(html);
        Assert.True(match.Success, $"Token antiforgery no encontrado en {url}.");
        return match.Groups[1].Value;
    }

    private static async Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string email)
    {
        var token = await GetFormTokenAsync(client, "/Account/Register");
        return await client.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["DisplayName"] = "Usuario Nuevo",
            ["Email"] = email,
            ["Password"] = ValidPassword,
            ["ConfirmPassword"] = ValidPassword,
            ["__RequestVerificationToken"] = token
        }));
    }

    private static async Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        var token = await GetFormTokenAsync(client, "/Account/Login");
        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["RememberMe"] = "false",
            ["ReturnUrl"] = "",
            ["__RequestVerificationToken"] = token
        }));
    }

    [Fact]
    public async Task Responses_IncludeSecurityHeaders()
    {
        await using var factory = CreateFactory(NewDbPath());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var nosniff));
        Assert.Contains("nosniff", nosniff);

        Assert.True(response.Headers.TryGetValues("X-Frame-Options", out var frame));
        Assert.Contains("DENY", frame);

        Assert.True(response.Headers.TryGetValues("Referrer-Policy", out var referrer));
        Assert.Contains("strict-origin-when-cross-origin", referrer);

        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var csp));
        Assert.Contains(csp, v => v.Contains("default-src 'self'") && v.Contains("frame-ancestors 'self'"));
    }

    [Fact]
    public async Task UnknownHost_IsRejected()
    {
        await using var factory = CreateFactory(NewDbPath());
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");
        request.Headers.Host = "evil.example.com";

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_RateLimit_Returns429AfterFiveAttempts()
    {
        await using var factory = CreateFactory(NewDbPath());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var allowed = await PostLoginAsync(client, "spam@example.com", "Contraseña#Incorrecta");
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        var rejected = await PostLoginAsync(client, "spam@example.com", "Contraseña#Incorrecta");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    [Fact]
    public async Task UnconfirmedUser_CannotLogin()
    {
        await using var factory = CreateFactory(NewDbPath());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await RegisterAsync(client, "sinconfirmar@example.com");

        var login = await PostLoginAsync(client, "sinconfirmar@example.com", ValidPassword);
        var loginHtml = await login.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains("revisa que hayas confirmado tu correo", WebUtility.HtmlDecode(loginHtml));

        var projects = await client.GetAsync("/Projects");
        Assert.Equal(HttpStatusCode.Redirect, projects.StatusCode);
        Assert.Contains("/Account/Login", projects.Headers.Location?.ToString());
    }

    [Fact]
    public async Task ConfirmationLink_ConfirmsEmail_AndOpensSession()
    {
        await using var factory = CreateFactory(NewDbPath());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var register = await RegisterAsync(client, "confirmar@example.com");
        Assert.Equal(HttpStatusCode.Redirect, register.StatusCode);

        var confirmationPage = await client.GetStringAsync(register.Headers.Location!.ToString());
        var decoded = WebUtility.HtmlDecode(confirmationPage);
        var match = ConfirmationLinkRegex.Match(decoded);
        Assert.True(match.Success, "Enlace de confirmación no encontrado en el entorno de desarrollo.");

        var confirm = await client.GetAsync(match.Groups[1].Value);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var projects = await client.GetAsync("/Projects");
        Assert.Equal(HttpStatusCode.OK, projects.StatusCode);
    }
}