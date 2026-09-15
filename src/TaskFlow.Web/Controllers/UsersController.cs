using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Core.Domain.Authorization;
using TaskFlow.Data;
using TaskFlow.Web.Authorization;
using TaskFlow.Web.Models.Admin;

namespace TaskFlow.Web.Controllers;

[Authorize(Policy = Policies.AdminOnly)]
public class UsersController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _db;

    public UsersController(UserManager<AppUser> userManager, AppDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var adminRoleId = await _db.Roles
            .Where(r => r.Name == ApplicationRoles.Admin)
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        var rows = await _userManager.Users
            .OrderBy(u => u.Email)
            .Select(u => new UserListItemViewModel
            {
                Id = u.Id,
                Email = u.Email ?? string.Empty,
                DisplayName = u.DisplayName,
                CreatedAtUtc = u.CreatedAtUtc,
                IsAdministrator = adminRoleId != null
                    && _db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == adminRoleId)
            })
            .ToListAsync();

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

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var currentAdmins = await _userManager.GetUsersInRoleAsync(ApplicationRoles.Admin);
        if (currentAdmins.Count <= 1)
        {
            await transaction.RollbackAsync();
            TempData["Error"] = "No se puede retirar el rol al último administrador del sistema.";
            return RedirectToAction(nameof(Index));
        }

        var removeResult = await _userManager.RemoveFromRoleAsync(target, ApplicationRoles.Admin);
        await transaction.CommitAsync();
        TempData["Success"] = removeResult.Succeeded
            ? "Rol de administrador retirado a " + target.Email + "."
            : "No se pudo retirar el rol de administrador.";

        return RedirectToAction(nameof(Index));
    }
}