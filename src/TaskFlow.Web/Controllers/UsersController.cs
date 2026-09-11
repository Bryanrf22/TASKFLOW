using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Core.Domain.Authorization;
using TaskFlow.Data;
using TaskFlow.Web.Authorization;
using TaskFlow.Web.Models.Admin;

namespace TaskFlow.Web.Controllers;

[Authorize(Policy = Policies.AdminOnly)]
public class UsersController : Controller
{
    private readonly UserManager<AppUser> _userManager;

    public UsersController(UserManager<AppUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var users = _userManager.Users.OrderBy(u => u.Email).ToList();

        var rows = new List<UserListItemViewModel>(users.Count);
        foreach (var user in users)
        {
            rows.Add(new UserListItemViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName,
                CreatedAtUtc = user.CreatedAtUtc,
                IsAdministrator = await _userManager.IsInRoleAsync(user, ApplicationRoles.Admin)
            });
        }

        return View(new UsersListViewModel { Users = rows });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleAdmin(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest();

        var target = await _userManager.FindByIdAsync(userId);
        if (target is null)
            return NotFound();

        var currentUserId = _userManager.GetUserId(User) ?? string.Empty;
        var isAdmin = await _userManager.IsInRoleAsync(target, ApplicationRoles.Admin);

        if (!isAdmin)
        {
            var result = await _userManager.AddToRoleAsync(target, ApplicationRoles.Admin);
            TempData["Success"] = result.Succeeded
                ? "Rol de administrador asignado a " + target.Email + "."
                : "No se pudo asignar el rol de administrador.";
            return RedirectToAction(nameof(Index));
        }

        if (string.Equals(target.Id, currentUserId, StringComparison.Ordinal))
        {
            TempData["Error"] = "No puedes retirarte tu propio rol de administrador.";
            return RedirectToAction(nameof(Index));
        }

        var admins = await _userManager.GetUsersInRoleAsync(ApplicationRoles.Admin);
        if (admins.Count <= 1)
        {
            TempData["Error"] = "No se puede retirar el rol al último administrador del sistema.";
            return RedirectToAction(nameof(Index));
        }

        var removeResult = await _userManager.RemoveFromRoleAsync(target, ApplicationRoles.Admin);
        TempData["Success"] = removeResult.Succeeded
            ? "Rol de administrador retirado a " + target.Email + "."
            : "No se pudo retirar el rol de administrador.";

        return RedirectToAction(nameof(Index));
    }
}