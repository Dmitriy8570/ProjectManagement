using System.Security.Claims;
using System.Text.Encodings.Web;
using BusinessLogic.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Tests.Presentation.E2E;

/// <summary>
/// Authentication scheme registered by <see cref="ApiFactory"/> in place of the
/// JWT bearer pipeline. Reads two simple request headers — <c>X-Test-Role</c>
/// and <c>X-Test-EmployeeId</c> — and synthesizes a <see cref="ClaimsPrincipal"/>
/// with the same claim shape the real <c>JwtTokenService</c> produces
/// (<see cref="ClaimTypes.Role"/>, <see cref="ClaimTypes.NameIdentifier"/> and
/// the custom <c>EmployeeId</c> claim). Lets each test assert role-gated
/// behavior without spinning up the full Identity + token flow.
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

        // Default to a synthetic user id so SignInManager-style lookups don't trip.
        var userId = $"test-user-{(string)roleValues!}";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userId)
        };

        // RoleHeader is multi-valued so a test can grant several roles to the
        // same caller (matches the way ASP.NET Identity stamps multiple Role
        // claims on the principal).
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

    private static IEnumerable<string> SplitRoles(StringValues raw) =>
        raw
            .SelectMany(v => (v ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(v => !string.IsNullOrWhiteSpace(v));
}

/// <summary>
/// Convenience methods that stamp the test-auth headers onto an outgoing
/// request. Each test class clones a base <see cref="HttpClient"/> per role so
/// concurrent xUnit tests don't fight over the shared header collection.
/// </summary>
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

    /// <summary>Sends no auth headers so the request hits the fallback policy.</summary>
    public static HttpClient AsAnonymous(this HttpClient client)
    {
        client.DefaultRequestHeaders.Remove(TestAuthHandler.RoleHeader);
        client.DefaultRequestHeaders.Remove(TestAuthHandler.EmployeeIdHeader);
        return client;
    }
}
