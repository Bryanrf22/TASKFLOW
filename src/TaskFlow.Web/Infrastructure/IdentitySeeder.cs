using Microsoft.AspNetCore.Identity;
using TaskFlow.Core.Domain.Authorization;
using TaskFlow.Data;

namespace TaskFlow.Web.Infrastructure;

public sealed class IdentitySeeder
{
    private const string DefaultAdminEmail = "admin@taskflow.local";
    private const string DefaultAdminPassword = "Admin123!";

    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IdentitySeeder> _logger;

    public IdentitySeeder(
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        ILogger<IdentitySeeder> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        if (!await _roleManager.RoleExistsAsync(ApplicationRoles.Admin))
        {
            var roleResult = await _roleManager.CreateAsync(new IdentityRole(ApplicationRoles.Admin));
            if (!roleResult.Succeeded)
            {
                _logger.LogError("No se pudo crear el rol {Role}: {Errors}", ApplicationRoles.Admin, string.Join("; ", roleResult.Errors.Select(e => e.Description)));
                return;
            }
            _logger.LogInformation("Rol {Role} creado", ApplicationRoles.Admin);
        }

        var email = _configuration["Seed:AdminEmail"];
        var password = _configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        if (string.Equals(email, DefaultAdminEmail, StringComparison.OrdinalIgnoreCase)
            && string.Equals(password, DefaultAdminPassword, StringComparison.Ordinal))
        {
            _logger.LogWarning("Se está usando la cuenta de administrador por defecto de desarrollo ({Email}). Cámbiala para entornos de publicación.", email);
        }

        var admin = await _userManager.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new AppUser
            {
                UserName = email,
                Email = email,
                DisplayName = "Administrador",
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(admin, password);
            if (!createResult.Succeeded)
            {
                _logger.LogError("No se pudo crear el usuario administrador {Email}: {Errors}", email, string.Join("; ", createResult.Errors.Select(e => e.Description)));
                return;
            }
            _logger.LogInformation("Usuario administrador {Email} creado", email);
        }

        if (!await _userManager.IsInRoleAsync(admin, ApplicationRoles.Admin))
        {
            var addResult = await _userManager.AddToRoleAsync(admin, ApplicationRoles.Admin);
            if (!addResult.Succeeded)
            {
                _logger.LogError("No se pudo asignar el rol {Role} a {Email}: {Errors}", ApplicationRoles.Admin, email, string.Join("; ", addResult.Errors.Select(e => e.Description)));
            }
        }
    }
}