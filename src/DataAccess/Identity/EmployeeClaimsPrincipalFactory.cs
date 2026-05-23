using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DataAccess.Identity;

/// <summary>
/// Adds a custom <c>EmployeeId</c> claim to the principal when the user signs
/// in. <see cref="BusinessLogic.Identity.ICurrentUserService"/> reads this
/// claim to map the authenticated user back to a domain Employee — no
/// per-request database round-trip.
/// </summary>
public class EmployeeClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public const string EmployeeIdClaim = "EmployeeId";

    public EmployeeClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(EmployeeIdClaim, user.EmployeeId.ToString()));
        return identity;
    }
}
