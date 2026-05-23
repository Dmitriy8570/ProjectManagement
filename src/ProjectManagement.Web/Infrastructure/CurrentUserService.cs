using System.Security.Claims;
using BusinessLogic.Identity;
using DataAccess.Identity;

namespace ProjectManagement.Web.Infrastructure;

/// <summary>
/// Resolves <see cref="ICurrentUserService"/> from the active HTTP request.
/// All data comes from claims set during sign-in (NameIdentifier, role,
/// the custom EmployeeId claim from <see cref="EmployeeClaimsPrincipalFactory"/>)
/// — no DB round-trip per request.
/// </summary>
internal sealed class CurrentUserService : ICurrentUserService
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
            var raw = Principal?.FindFirstValue(EmployeeClaimsPrincipalFactory.EmployeeIdClaim);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;
}
