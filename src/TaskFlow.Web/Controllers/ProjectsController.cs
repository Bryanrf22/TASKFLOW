using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Core.Domain.Enums;
using TaskFlow.Data;
using TaskFlow.Web.Models.Projects;
using TaskFlow.Web.Models.Tasks;
using TaskFlow.Web.Services;

namespace TaskFlow.Web.Controllers;

[Route("Projects")]
public class ProjectsController : Controller
{
    private readonly ProjectService _projects;
    private readonly TaskService _tasks;
    private readonly UserManager<AppUser> _userManager;

    public ProjectsController(ProjectService projects, TaskService tasks, UserManager<AppUser> userManager)
    {
        _projects = projects;
        _tasks = tasks;
        _userManager = userManager;
    }

    private string CurrentUserId()
        => _userManager.GetUserId(User) ?? throw new InvalidOperationException("Usuario sin identificador.");

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        return View(await BuildIndexAsync());
    }

    [HttpPost("")]
    public async Task<IActionResult> Create(CreateProjectViewModel model)
    {
        if (!ModelState.IsValid)
            return View("Index", await BuildIndexAsync(model));

        var result = await _projects.CreateAsync(CurrentUserId(), model);
        if (!result.Succeeded)
        {
            if (result.Status == OperationStatus.Conflict)
            {
                ModelState.AddModelError(nameof(model.Key), "Ya existe un proyecto con esa clave.");
            }
            return View("Index", await BuildIndexAsync(model));
        }

        TempData["Success"] = "Proyecto creado.";
        return RedirectToAction(nameof(Details), new { projectId = result.ValueOrThrow });
    }

    [HttpGet("{projectId:guid}")]
    public async Task<IActionResult> Details(Guid projectId)
    {
        var result = await _projects.GetDetailsAsync(CurrentUserId(), projectId);
        if (result.Status == OperationStatus.NotFound)
            return NotFound();

        return View(result.ValueOrThrow);
    }

    [HttpPost("{projectId:guid}/Edit")]
    public async Task<IActionResult> Edit(Guid projectId, EditProjectViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Datos inválidos.";
            return RedirectToAction(nameof(Details), new { projectId });
        }

        var result = await _projects.UpdateSettingsAsync(CurrentUserId(), projectId, model);
        if (!result.Succeeded)
            return FromOperation(result);

        TempData["Success"] = "Proyecto actualizado.";
        return RedirectToAction(nameof(Details), new { projectId });
    }

    [HttpPost("{projectId:guid}/Delete")]
    public async Task<IActionResult> Delete(Guid projectId)
    {
        var result = await _projects.DeleteAsync(CurrentUserId(), projectId);
        if (!result.Succeeded)
            return FromOperation(result);

        TempData["Success"] = "Proyecto eliminado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{projectId:guid}/Members/Add")]
    public async Task<IActionResult> AddMember(Guid projectId, CreateMemberViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Datos inválidos.";
            return RedirectToAction(nameof(Details), new { projectId });
        }

        var result = await _projects.AddMemberAsync(CurrentUserId(), projectId, model);
        switch (result.Status)
        {
            case OperationStatus.NotFound:
                return NotFound();
            case OperationStatus.Forbidden:
                return StatusCode(StatusCodes.Status403Forbidden);
            case OperationStatus.Invalid:
                TempData["Error"] = "No se encontró ninguna cuenta con ese email.";
                break;
            case OperationStatus.Conflict:
                TempData["Error"] = "Esa cuenta ya es miembro del proyecto.";
                break;
            default:
                TempData["Success"] = "Miembro añadido.";
                break;
        }

        return RedirectToAction(nameof(Details), new { projectId });
    }

    [HttpPost("{projectId:guid}/Members/{userId}/Role")]
    public async Task<IActionResult> ChangeMemberRole(Guid projectId, string userId, ProjectRole newRole)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        if (!Enum.IsDefined(typeof(ProjectRole), newRole))
            return BadRequest();

        var result = await _projects.ChangeMemberRoleAsync(CurrentUserId(), projectId, userId, newRole);
        switch (result.Status)
        {
            case OperationStatus.NotFound:
                return NotFound();
            case OperationStatus.Forbidden:
                return StatusCode(StatusCodes.Status403Forbidden);
            case OperationStatus.Invalid:
                return BadRequest();
            case OperationStatus.Conflict:
                TempData["Error"] = "El proyecto debe conservar al menos un Owner.";
                break;
            default:
                TempData["Success"] = "Rol de miembro actualizado.";
                break;
        }

        return RedirectToAction(nameof(Details), new { projectId });
    }

    [HttpPost("{projectId:guid}/Members/{userId}/Remove")]
    public async Task<IActionResult> RemoveMember(Guid projectId, string userId)
    {
        var result = await _projects.RemoveMemberAsync(CurrentUserId(), projectId, userId);
        switch (result.Status)
        {
            case OperationStatus.NotFound:
                return NotFound();
            case OperationStatus.Forbidden:
                return StatusCode(StatusCodes.Status403Forbidden);
            case OperationStatus.Conflict:
                TempData["Error"] = "El proyecto debe conservar al menos un Owner.";
                break;
            default:
                TempData["Success"] = "Miembro eliminado del proyecto.";
                break;
        }

        return RedirectToAction(nameof(Details), new { projectId });
    }

    [HttpPost("{projectId:guid}/Tasks")]
    public async Task<IActionResult> CreateTask(Guid projectId, CreateTaskViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Datos inválidos.";
            return RedirectToAction(nameof(Details), new { projectId });
        }

        var result = await _tasks.CreateAsync(CurrentUserId(), projectId, model);
        switch (result.Status)
        {
            case OperationStatus.NotFound:
                return NotFound();
            case OperationStatus.Forbidden:
                return StatusCode(StatusCodes.Status403Forbidden);
            case OperationStatus.Invalid:
                TempData["Error"] = "El usuario asignado debe ser miembro del proyecto.";
                return RedirectToAction(nameof(Details), new { projectId });
            default:
                TempData["Success"] = "Tarea creada.";
                return RedirectToAction(nameof(TasksController.Details), "Tasks", new { id = result.ValueOrThrow });
        }
    }

    private async Task<ProjectsIndexViewModel> BuildIndexAsync(CreateProjectViewModel? create = null)
        => new()
        {
            Projects = await _projects.ListForUserAsync(CurrentUserId()),
            Create = create ?? new CreateProjectViewModel()
        };

    private IActionResult FromOperation(OperationResult result)
        => result.Status switch
        {
            OperationStatus.NotFound => NotFound(),
            OperationStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden),
            _ => RedirectToAction(nameof(Index))
        };
}