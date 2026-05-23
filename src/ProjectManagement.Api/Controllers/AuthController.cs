using DataAccess.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ProjectManagement.Api.Dtos.Auth;
using ProjectManagement.Api.Infrastructure;

namespace ProjectManagement.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IJwtTokenService _jwt;

    public AuthController(UserManager<ApplicationUser> users, IJwtTokenService jwt)
    {
        _users = users;
        _jwt = jwt;
    }

    /// <summary>
    /// Verifies credentials and returns a signed JWT plus a digest of the
    /// caller's identity (roles, linked employee id). The SPA caches the
    /// token and sends it back as <c>Authorization: Bearer &lt;token&gt;</c>.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await _users.FindByEmailAsync(request.Email);
        // Same response for "no such user" and "wrong password" so the
        // endpoint doesn't double as a user-enumeration oracle.
        if (user is null || !await _users.CheckPasswordAsync(user, request.Password))
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid credentials"
            });

        var (token, expires) = await _jwt.GenerateAsync(user, ct);
        var roles = await _users.GetRolesAsync(user);

        return Ok(new LoginResponse
        {
            Token = token,
            ExpiresAtUtc = expires,
            User = new CurrentUserDto
            {
                Id = user.Id,
                Email = user.Email ?? "",
                EmployeeId = user.EmployeeId,
                Roles = roles.ToArray()
            }
        });
    }

    /// <summary>
    /// Echoes back the caller's identity decoded from the bearer token.
    /// Useful after page reloads so the SPA can rehydrate the user state
    /// without storing it locally.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserDto>> Me()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var roles = await _users.GetRolesAsync(user);
        return Ok(new CurrentUserDto
        {
            Id = user.Id,
            Email = user.Email ?? "",
            EmployeeId = user.EmployeeId,
            Roles = roles.ToArray()
        });
    }

    /// <summary>
    /// Stateless logout: there's nothing to invalidate on the server for a
    /// pure-bearer scheme, so the SPA simply drops the token. This endpoint
    /// exists for API symmetry and to give clients a place to PUT cleanup
    /// logic later (e.g. revocation lists, refresh-token rotation).
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout() => NoContent();
}
