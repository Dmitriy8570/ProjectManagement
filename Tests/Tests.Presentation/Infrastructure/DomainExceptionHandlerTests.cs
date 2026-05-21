using BusinessLogic.Common;
using Microsoft.AspNetCore.Http;
using ProjectManagement.Api.Infrastructure;

namespace Tests.Presentation.Infrastructure;

/// <summary>
/// Unit tests for DomainExceptionHandler in isolation. The E2E tests exercise
/// the same handler through the real pipeline; these focus on the exact
/// ProblemDetails content format which is harder to pin down end-to-end.
/// </summary>
public class DomainExceptionHandlerTests
{
    private readonly DomainExceptionHandler _handler = new();

    private static DefaultHttpContext CreateContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    [Fact]
    public async Task EntityNotFoundException_Returns404WithResourceNotFoundProblem()
    {
        var ctx       = CreateContext();
        var exception = new EntityNotFoundException("Project", 42);

        var handled = await _handler.TryHandleAsync(ctx, exception, CancellationToken.None);

        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.Response.Body)
            .ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
        Assert.Contains("Resource not found", body);
        Assert.Contains(exception.Message, body);
    }

    [Fact]
    public async Task DomainValidationException_Returns400WithValidationFailedProblem()
    {
        var ctx       = CreateContext();
        var exception = new DomainValidationException("Name is required.");

        var handled = await _handler.TryHandleAsync(ctx, exception, CancellationToken.None);

        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.Response.Body)
            .ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, ctx.Response.StatusCode);
        Assert.Contains("Validation failed", body);
        Assert.Contains("Name is required.", body);
    }

    // ReturnsFalse means the next IExceptionHandler in the pipeline will run,
    // eventually falling through to the default 500 handler.
    [Fact]
    public async Task UnrecognisedException_ReturnsFalseWithoutTouchingResponse()
    {
        var ctx          = CreateContext();
        var statusBefore = ctx.Response.StatusCode;

        var handled = await _handler.TryHandleAsync(
            ctx, new InvalidOperationException("unexpected"), CancellationToken.None);

        Assert.False(handled);
        Assert.Equal(statusBefore, ctx.Response.StatusCode);
    }
}

