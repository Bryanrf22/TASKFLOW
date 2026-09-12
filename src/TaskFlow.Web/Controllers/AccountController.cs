using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskFlow.Data;
using TaskFlow.Web.Infrastructure;
using TaskFlow.Web.Models.Accounts;

namespace TaskFlow.Web.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly SignInManager<AppUser> _signInManager;
    private readonly UserManager<AppUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<AppUser> signInManager,
        UserManager<AppUser> userManager,
        IEmailSender emailSender,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _emailSender = emailSender;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    private bool RequireEmailConfirmation
        => _configuration.GetValue("Security:RequireEmailConfirmation", true);

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [EnableRateLimiting("auth-brute-force")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Email o contraseña incorrectos.");
            return View(model);
        }

        if (RequireEmailConfirmation && !user.EmailConfirmed)
        {
            ModelState.AddModelError(string.Empty, "Email o contraseña incorrectos. Si son correctos, revisa que hayas confirmado tu correo.");
            return View(model);
        }

        var signIn = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
        if (signIn.Succeeded)
        {
            await _signInManager.SignInAsync(user, isPersistent: model.RememberMe);
            _logger.LogInformation("Sesión iniciada por {Email}", model.Email);
            return RedirectToLocal(model.ReturnUrl);
        }

        if (signIn.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Tu cuenta está bloqueada temporalmente por demasiados intentos fallidos. Inténtalo de nuevo en unos minutos.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Email o contraseña incorrectos.");
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [EnableRateLimiting("auth-brute-force")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = new AppUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.DisplayName.Trim(),
            EmailConfirmed = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == "DuplicateEmail"))
            {
                return RedirectToAction(nameof(RegisterConfirmation), new { email = user.Email });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        _logger.LogInformation("Nueva cuenta registrada: {Email}", user.Email);

        if (RequireEmailConfirmation)
        {
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var callbackUrl = Url.Action(nameof(ConfirmEmail), "Account", new { userId = user.Id, token = code }, Request.Scheme);
            await _emailSender.SendEmailAsync(user.Email!, "Confirma tu cuenta de TaskFlow", $"Confirma tu cuenta con este enlace: {callbackUrl}");

            return RedirectToAction(nameof(RegisterConfirmation), new { email = user.Email });
        }

        user.EmailConfirmed = true;
        await _userManager.UpdateAsync(user);
        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    [HttpGet]
    public async Task<IActionResult> RegisterConfirmation(string? email = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            return RedirectToAction(nameof(Login));

        ViewData["ShowDevelopmentLink"] = _environment.IsDevelopment();
        if (_environment.IsDevelopment())
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user is not null && !user.EmailConfirmed)
            {
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                ViewData["EmailConfirmationUrl"] = Url.Action(
                    nameof(ConfirmEmail),
                    "Account",
                    new { userId = user.Id, token = code },
                    Request.Scheme);
            }
        }

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string userId, string token)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            return RedirectToAction(nameof(Login));

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return RedirectToAction(nameof(Login));

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
            return RedirectToAction(nameof(Login));

        _logger.LogInformation("Email confirmado para {Email}", user.Email);
        await _signInManager.SignInAsync(user, isPersistent: false);
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(HomeController.Index), "Home");
    }
}