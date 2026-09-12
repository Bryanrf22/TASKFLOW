using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Data;
using TaskFlow.Web.Models.Tasks;
using TaskFlow.Web.Services;

namespace TaskFlow.Web.Controllers;

[Route("Tasks")]
public class TasksController : Controller
{
    private readonly TaskService _tasks;
    private readonly UserManager<AppUser> _userManager;

    public TasksController(TaskService tasks, UserManager<AppUser> userManager)
    {
        _tasks = tasks;
        _userManager = userManager;
    }

    private string CurrentUserId()
        => _userManager.GetUserId(User) ?? throw new InvalidOperationException("Usuario sin identificador.");

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var result = await _tasks.GetDetailsAsync(CurrentUserId(), id);
        if (result.Status == OperationStatus.NotFound)
            return NotFound();
        if (result.Status == OperationStatus.Forbidden)
            return StatusCode(StatusCodes.Status403Forbidden);

        return View(result.ValueOrThrow);
    }

    [HttpPost("{id:guid}/Edit")]
    public async Task<IActionResult> Edit(Guid id, EditTaskViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Datos inválidos.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = await _tasks.UpdateAsync(CurrentUserId(), id, model);
        switch (result.Status)
        {
            case OperationStatus.NotFound:
                return NotFound();
            case OperationStatus.Forbidden:
                return StatusCode(StatusCodes.Status403Forbidden);
            case OperationStatus.Invalid:
                TempData["Error"] = "El usuario asignado debe ser miembro del proyecto.";
                return RedirectToAction(nameof(Details), new { id });
            default:
                TempData["Success"] = "Tarea actualizada.";
                return RedirectToAction(nameof(Details), new { id });
        }
    }

    [HttpPost("{id:guid}/Delete")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _tasks.DeleteAsync(CurrentUserId(), id);
        if (result.Status == OperationStatus.NotFound)
            return NotFound();
        if (result.Status == OperationStatus.Forbidden)
            return StatusCode(StatusCodes.Status403Forbidden);

        TempData["Success"] = "Tarea eliminada.";
        return RedirectToAction(nameof(ProjectsController.Details), "Projects", new { projectId = result.ValueOrThrow });
    }

    [HttpPost("{id:guid}/Comments")]
    public async Task<IActionResult> AddComment(Guid id, AddTaskCommentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "El comentario no puede estar vacío.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = await _tasks.AddCommentAsync(CurrentUserId(), id, model);
        if (result.Status == OperationStatus.NotFound)
            return NotFound();
        if (result.Status == OperationStatus.Forbidden)
            return StatusCode(StatusCodes.Status403Forbidden);

        return RedirectToAction(nameof(Details), new { id });
    }
}