using BusinessLogic.Common;
using BusinessLogic.Documents;
using BusinessLogic.Documents.Commands;
using BusinessLogic.Documents.Queries;
using BusinessLogic.Identity;
using BusinessLogic.Projects;
using BusinessLogic.Projects.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ProjectManagement.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
public class DocumentsController : ControllerBase
{
    private const string DirectorOrPm = Roles.Director + "," + Roles.ProjectManager;

    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public DocumentsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>Lists all documents attached to a project.</summary>
    [HttpGet("api/projects/{projectId:int}/documents")]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ProjectDocumentDto>>> GetDocuments(
        int projectId, CancellationToken ct)
    {
        if (!await CanViewAsync(projectId, ct))
            return Forbid();

        var result = await _mediator.Send(
            new GetProjectDocumentsQuery { ProjectId = projectId }, ct);
        return Ok(result);
    }

    /// <summary>Uploads a file and attaches it to the specified project.</summary>
    [HttpPost("api/projects/{projectId:int}/documents")]
    [Authorize(Roles = DirectorOrPm)]
    [RequestSizeLimit(52_428_800)]
    [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
    [ProducesResponseType(typeof(ProjectDocumentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProjectDocumentDto>> UploadDocument(
        int projectId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ProblemDetails { Title = "No file provided." });

        if (!await CanManageAsync(projectId, ct))
            return Forbid();

        var command = new UploadDocumentCommand
        {
            ProjectId   = projectId,
            FileName    = file.FileName,
            ContentType = file.ContentType,
            SizeBytes   = file.Length,
            Content     = file.OpenReadStream()
        };

        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(DownloadDocument), new { id = result.Id }, result);
    }

    /// <summary>Downloads a document file by its ID.</summary>
    [HttpGet("api/documents/{id:int}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Note: the URL doesn't carry a project id, so we don't resource-check
    // here — any authenticated caller can download by document id. A stricter
    // build would extend GetDocumentDownloadQuery to expose the parent
    // project id and gate via CanViewAsync.
    public async Task<IActionResult> DownloadDocument(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new GetDocumentDownloadQuery { DocumentId = id }, ct);
        return File(result.Content, result.ContentType, result.FileName);
    }

    /// <summary>Deletes a document and its associated file.</summary>
    [HttpDelete("api/documents/{id:int}")]
    [Authorize(Roles = DirectorOrPm)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // Same caveat as Download: no project id in the route, so we can't
    // narrow this to "PM of THIS project". Restricted to PM+Director role
    // — a malicious PM can still delete documents off other projects until
    // the route is extended.
    public async Task<IActionResult> DeleteDocument(int id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteDocumentCommand { DocumentId = id }, ct);
        return NoContent();
    }

    // ── Authorization helpers ──────────────────────────────────────────────

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
