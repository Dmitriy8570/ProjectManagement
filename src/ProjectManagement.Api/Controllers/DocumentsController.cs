using BusinessLogic.Documents;
using BusinessLogic.Documents.Commands;
using BusinessLogic.Documents.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ProjectManagement.Api.Controllers;

[ApiController]
[Produces("application/json")]
public sealed class DocumentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public DocumentsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lists all documents attached to a project.</summary>
    [HttpGet("api/projects/{projectId:int}/documents")]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectDocumentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProjectDocumentDto>>> GetDocuments(
        int projectId, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new GetProjectDocumentsQuery { ProjectId = projectId }, ct);
        return Ok(result);
    }

    /// <summary>Uploads a file and attaches it to the specified project.</summary>
    [HttpPost("api/projects/{projectId:int}/documents")]
    [RequestSizeLimit(52_428_800)]
    [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
    [ProducesResponseType(typeof(ProjectDocumentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectDocumentDto>> UploadDocument(
        int projectId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ProblemDetails { Title = "No file provided." });

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
    public async Task<IActionResult> DownloadDocument(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new GetDocumentDownloadQuery { DocumentId = id }, ct);
        return File(result.Content, result.ContentType, result.FileName);
    }

    /// <summary>Deletes a document and its associated file.</summary>
    [HttpDelete("api/documents/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDocument(int id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteDocumentCommand { DocumentId = id }, ct);
        return NoContent();
    }
}
