using BusinessLogic.Common;
using BusinessLogic.Documents.Commands;
using BusinessLogic.Documents.Queries;
using BusinessLogic.Identity;
using BusinessLogic.Projects;
using BusinessLogic.Projects.Commands;
using BusinessLogic.Projects.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagement.Web.ViewModels.Projects;

namespace ProjectManagement.Web.Controllers;

[Route("projects")]
[Authorize]
// Role rules (per spec):
//   Руководитель      — full access to every project.
//   Менеджер проекта  — sees and edits ONLY projects where they are the PM;
//                       can manage that project's participants and documents.
//   Сотрудник         — sees ONLY projects they participate in; read-only.
// Index filters the list at the query level (no flash of forbidden rows);
// write-side actions add a resource check after loading the project so URL
// guessing also returns 403.
public sealed class ProjectsController : Controller
{
    private const string DirectorOrPm = Roles.Director + "," + Roles.ProjectManager;

    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public ProjectsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet("")]
    [Route("~/")]
    public async Task<IActionResult> Index([FromQuery] ProjectListViewModel vm, CancellationToken ct)
    {
        vm.Page = vm.Page < 1 ? 1 : vm.Page;
        vm.PageSize = vm.PageSize is < 1 or > 100 ? 10 : vm.PageSize;

        var filter = new ProjectListFilter
        {
            NameSearch    = vm.NameSearch,
            StartDateFrom = vm.StartDateFrom,
            StartDateTo   = vm.StartDateTo,
            MinPriority   = vm.MinPriority,
            MaxPriority   = vm.MaxPriority,
            SortBy        = vm.SortBy,
            Descending    = vm.Descending,
            Page          = vm.Page,
            PageSize      = vm.PageSize
        };

        // Director sees everything; PM sees managed-by-self; Employee sees
        // participated-in. ProjectListFilter ANDs the two id criteria, so
        // pick at most one — multi-role users prefer PM (broader rights).
        if (!User.IsInRole(Roles.Director))
        {
            var empId = _currentUser.EmployeeId;
            if (empId is null)
                return Forbid();

            if (User.IsInRole(Roles.ProjectManager))
                filter = filter with { ProjectManagerId = empId };
            else
                filter = filter with { ParticipantEmployeeId = empId };
        }

        var result = await _mediator.Send(new GetProjectsQuery { Filter = filter }, ct);
        vm.Items = result.Items;
        vm.TotalCount = result.TotalCount;
        return View(vm);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        try
        {
            var projectTask  = _mediator.Send(new GetProjectByIdQuery { Id = id }, ct);
            var documentsTask = _mediator.Send(new GetProjectDocumentsQuery { ProjectId = id }, ct);
            await Task.WhenAll(projectTask, documentsTask);

            var project = projectTask.Result;
            if (!CanView(project))
                return Forbid();

            ViewBag.Documents = documentsTask.Result;
            return View(project);
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:int}/documents/upload")]
    [Authorize(Roles = DirectorOrPm)]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(52_428_800)]
    [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
    public async Task<IActionResult> UploadDocument(
        int id, List<IFormFile>? files, CancellationToken ct)
    {
        if (!await CanManageAsync(id, ct))
            return Forbid();

        var validFiles = files?.Where(f => f.Length > 0).ToList();
        if (validFiles is not { Count: > 0 })
        {
            TempData["Error"] = "Please select at least one file to upload.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var errors = new List<string>();
        var uploaded = 0;

        foreach (var file in validFiles)
        {
            try
            {
                await _mediator.Send(new UploadDocumentCommand
                {
                    ProjectId   = id,
                    FileName    = file.FileName,
                    ContentType = file.ContentType,
                    SizeBytes   = file.Length,
                    Content     = file.OpenReadStream()
                }, ct);
                uploaded++;
            }
            catch (DomainValidationException ex)
            {
                errors.Add($"\"{file.FileName}\": {ex.Message}");
            }
        }

        if (uploaded > 0)
            TempData["Success"] = uploaded == 1
                ? $"File \"{validFiles[0].FileName}\" uploaded."
                : $"{uploaded} files uploaded.";

        if (errors.Count > 0)
            TempData["Error"] = string.Join(" | ", errors);

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpGet("{id:int}/documents/{docId:int}/download")]
    public async Task<IActionResult> DownloadDocument(int id, int docId, CancellationToken ct)
    {
        // Download follows the same view-side rule as Detail — participants
        // can pull files off projects they belong to.
        if (!await CanViewAsync(id, ct))
            return Forbid();

        try
        {
            var result = await _mediator.Send(
                new GetDocumentDownloadQuery { DocumentId = docId }, ct);
            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:int}/documents/{docId:int}/delete")]
    [Authorize(Roles = DirectorOrPm)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDocument(int id, int docId, CancellationToken ct)
    {
        if (!await CanManageAsync(id, ct))
            return Forbid();

        try
        {
            await _mediator.Send(new DeleteDocumentCommand { DocumentId = docId }, ct);
            TempData["Success"] = "Document deleted.";
        }
        catch (EntityNotFoundException)
        {
            TempData["Error"] = "Document not found.";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpGet("create")]
    [Authorize(Roles = Roles.Director)]
    public IActionResult Create()
    {
        return View(new CreateProjectViewModel
        {
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddMonths(3)
        });
    }

    [HttpPost("create")]
    [Authorize(Roles = Roles.Director)]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(52_428_800)]
    [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
    public async Task<IActionResult> Create(
        CreateProjectViewModel model, List<IFormFile>? files, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var command = new CreateProjectCommand
            {
                Data = new CreateProjectRequest
                {
                    Name = model.Name,
                    CustomerCompany = model.CustomerCompany,
                    ExecutingCompany = model.ExecutingCompany,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    ProjectManagerId = model.ProjectManagerId,
                    EmployeeIds = model.EmployeeIds,
                    Priority = model.Priority
                }
            };
            var result = await _mediator.Send(command, ct);

            if (files is { Count: > 0 })
            {
                foreach (var file in files.Where(f => f.Length > 0))
                {
                    try
                    {
                        await _mediator.Send(new UploadDocumentCommand
                        {
                            ProjectId   = result.Id,
                            FileName    = file.FileName,
                            ContentType = file.ContentType,
                            SizeBytes   = file.Length,
                            Content     = file.OpenReadStream()
                        }, ct);
                    }
                    catch (DomainValidationException)
                    {
                        // Oversized files are skipped; the project is still created.
                    }
                }
            }

            TempData["Success"] = $"Project \"{model.Name}\" was created successfully.";
            return RedirectToAction(nameof(Detail), new { id = result.Id });
        }
        catch (DomainValidationException ex)
        {
            ModelState.AddModelError("", ex.Message);
        }
        catch (EntityNotFoundException ex)
        {
            ModelState.AddModelError("", ex.Message);
        }

        return View(model);
    }

    [HttpGet("{id:int}/edit")]
    [Authorize(Roles = DirectorOrPm)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        try
        {
            var project = await _mediator.Send(new GetProjectByIdQuery { Id = id }, ct);
            if (!CanManage(project))
                return Forbid();

            return View(new EditProjectViewModel
            {
                Id = id,
                Name = project.Name,
                CustomerCompany = project.CustomerCompany,
                ExecutingCompany = project.ExecutingCompany,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                Priority = project.Priority,
                ProjectManagerId = project.ProjectManager.Id,
                ProjectManagerName = project.ProjectManager.FullName
            });
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:int}/edit")]
    [Authorize(Roles = DirectorOrPm)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditProjectViewModel model, CancellationToken ct)
    {
        if (!await CanManageAsync(id, ct))
            return Forbid();

        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var command = new EditProjectCommand
            {
                Id = id,
                Data = new EditProjectRequest
                {
                    Name = model.Name,
                    CustomerCompany = model.CustomerCompany,
                    ExecutingCompany = model.ExecutingCompany,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    ProjectManagerId = model.ProjectManagerId,
                    Priority = model.Priority
                }
            };
            await _mediator.Send(command, ct);
            TempData["Success"] = "Project updated successfully.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        catch (DomainValidationException ex)
        {
            ModelState.AddModelError("", ex.Message);
        }
        catch (EntityNotFoundException ex)
        {
            ModelState.AddModelError("", ex.Message);
        }

        return View(model);
    }

    [HttpPost("{id:int}/delete")]
    [Authorize(Roles = Roles.Director)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new DeleteProjectCommand { Id = id }, ct);
            TempData["Success"] = "Project deleted.";
        }
        catch (EntityNotFoundException)
        {
            TempData["Error"] = "Project not found.";
        }
        return RedirectToAction(nameof(Index));
    }

    // AJAX endpoint for project name autocomplete on the index page.
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? term, CancellationToken ct)
    {
        var filter = new ProjectListFilter
        {
            NameSearch = term,
            PageSize   = 10,
            SortBy     = ProjectSortBy.Name
        };

        // Mirror the Index filter so the autocomplete dropdown never offers
        // a project the user wouldn't be allowed to open.
        if (!User.IsInRole(Roles.Director))
        {
            var empId = _currentUser.EmployeeId;
            if (empId is null)
                return Forbid();

            filter = User.IsInRole(Roles.ProjectManager)
                ? filter with { ProjectManagerId = empId }
                : filter with { ParticipantEmployeeId = empId };
        }

        var result = await _mediator.Send(new GetProjectsQuery { Filter = filter }, ct);
        return Json(result.Items.Select(p => new { p.Id, p.Name }));
    }

    [HttpPost("{id:int}/assign")]
    [Authorize(Roles = DirectorOrPm)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int id, int employeeId, CancellationToken ct)
    {
        if (!await CanManageAsync(id, ct))
            return Forbid();

        try
        {
            await _mediator.Send(new AssignEmployeeToProjectCommand
            {
                Data = new AssignEmployeeToProjectRequest { ProjectId = id, EmployeeId = employeeId }
            }, ct);
            TempData["Success"] = "Employee assigned to project.";
        }
        catch (DomainValidationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (EntityNotFoundException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/unassign")]
    [Authorize(Roles = DirectorOrPm)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unassign(int id, int employeeId, CancellationToken ct)
    {
        if (!await CanManageAsync(id, ct))
            return Forbid();

        try
        {
            await _mediator.Send(new UnassignEmployeeFromProjectCommand
            {
                Data = new UnassignEmployeeFromProjectRequest { ProjectId = id, EmployeeId = employeeId }
            }, ct);
            TempData["Success"] = "Employee removed from project.";
        }
        catch (DomainValidationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (EntityNotFoundException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Detail), new { id });
    }

    // ── Authorization helpers ────────────────────────────────────────────
    // Director: bypass. Other roles must be linked to the project either as
    // PM (CanManage) or as PM/participant (CanView).

    private bool CanView(ProjectDto project)
    {
        if (User.IsInRole(Roles.Director))
            return true;

        var empId = _currentUser.EmployeeId;
        if (empId is null)
            return false;

        return project.ProjectManager.Id == empId
            || project.Employees.Any(e => e.Id == empId);
    }

    private bool CanManage(ProjectDto project)
    {
        if (User.IsInRole(Roles.Director))
            return true;

        var empId = _currentUser.EmployeeId;
        return empId is not null && project.ProjectManager.Id == empId;
    }

    private async Task<bool> CanViewAsync(int projectId, CancellationToken ct)
    {
        if (User.IsInRole(Roles.Director))
            return true;
        try
        {
            var project = await _mediator.Send(new GetProjectByIdQuery { Id = projectId }, ct);
            return CanView(project);
        }
        catch (EntityNotFoundException)
        {
            return false;
        }
    }

    private async Task<bool> CanManageAsync(int projectId, CancellationToken ct)
    {
        if (User.IsInRole(Roles.Director))
            return true;
        try
        {
            var project = await _mediator.Send(new GetProjectByIdQuery { Id = projectId }, ct);
            return CanManage(project);
        }
        catch (EntityNotFoundException)
        {
            return false;
        }
    }
}
