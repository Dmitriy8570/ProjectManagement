using System.Security.Claims;
using System.Text.Encodings.Web;
using BusinessLogic.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Tests.Presentation.Web.E2E;

/// <summary>
/// Header-driven auth scheme that stands in for the production Identity cookie
/// pipeline during tests. Reads <c>X-Test-Role</c> and <c>X-Test-EmployeeId</c>
/// off the request and synthesizes a <see cref="ClaimsPrincipal"/> with the
/// same shape <c>EmployeeClaimsPrincipalFactory</c> produces. Lets each test
/// drive the controller's role rules without a real login round-trip.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string RoleHeader = "X-Test-Role";
    public const string EmployeeIdHeader = "X-Test-EmployeeId";
    public const string EmployeeIdClaim = "EmployeeId";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RoleHeader, out var roleValues))
            return Task.FromResult(AuthenticateResult.NoResult());

        var userId = $"test-user-{(string)roleValues!}";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userId)
        };

        foreach (var role in SplitRoles(roleValues))
            claims.Add(new Claim(ClaimTypes.Role, role));

        if (Request.Headers.TryGetValue(EmployeeIdHeader, out var empValues) &&
            int.TryParse(empValues.ToString(), out var empId))
        {
            claims.Add(new Claim(EmployeeIdClaim, empId.ToString()));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    // On a 401 the MVC pipeline normally redirects to /account/login; tests
    // assert raw status codes instead, so override the challenge to a plain
    // 401 — keeps assertions identical to the API project.
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    private static IEnumerable<string> SplitRoles(StringValues raw) =>
        raw
            .SelectMany(v => (v ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(v => !string.IsNullOrWhiteSpace(v));
}

public static class TestAuthClientExtensions
{
    public static HttpClient AsRole(this HttpClient client, string role, int? employeeId = null)
    {
        client.DefaultRequestHeaders.Remove(TestAuthHandler.RoleHeader);
        client.DefaultRequestHeaders.Remove(TestAuthHandler.EmployeeIdHeader);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
        if (employeeId.HasValue)
            client.DefaultRequestHeaders.Add(
                TestAuthHandler.EmployeeIdHeader, employeeId.Value.ToString());
        return client;
    }

    public static HttpClient AsDirector(this HttpClient client, int? employeeId = null) =>
        client.AsRole(Roles.Director, employeeId);

    public static HttpClient AsProjectManager(this HttpClient client, int employeeId) =>
        client.AsRole(Roles.ProjectManager, employeeId);

    public static HttpClient AsEmployee(this HttpClient client, int employeeId) =>
        client.AsRole(Roles.Employee, employeeId);

    public static HttpClient AsAnonymous(this HttpClient client)
    {
        client.DefaultRequestHeaders.Remove(TestAuthHandler.RoleHeader);
        client.DefaultRequestHeaders.Remove(TestAuthHandler.EmployeeIdHeader);
        return client;
    }
}
