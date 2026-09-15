using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Data;

namespace TaskFlow.Web.Controllers;

public abstract class AppController : Controller
{
    protected UserManager<AppUser> UserManager { get; }

    protected AppController(UserManager<AppUser> userManager)
    {
        UserManager = userManager;
    }

    protected string CurrentUserId()
        => UserManager.GetUserId(User) ?? throw new InvalidOperationException("Usuario sin identificador.");
}
