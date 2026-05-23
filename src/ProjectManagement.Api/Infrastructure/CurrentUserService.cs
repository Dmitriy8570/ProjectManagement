using System.Security.Claims;
using BusinessLogic.Identity;

namespace ProjectManagement.Api.Infrastructure;

/// <summary>
/// JWT-pipeline counterpart of the cookie-pipeline <c>CurrentUserService</c>
/// in the Web project. Reads the same claim set
/// (<see cref="ClaimTypes.NameIdentifier"/>, <see cref="ClaimTypes.Role"/>,
/// custom <c>EmployeeId</c>) — populated by <see cref="JwtTokenService"/>.
/// </summary>
internal class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public int? EmployeeId
    {
        get
        {
            var raw = Principal?.FindFirstValue(JwtTokenService.EmployeeIdClaim);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;
}
