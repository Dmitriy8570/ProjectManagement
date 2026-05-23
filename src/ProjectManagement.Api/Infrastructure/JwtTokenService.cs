using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DataAccess.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ProjectManagement.Api.Infrastructure;

/// <summary>
/// Issues short-lived HS256 JWTs that carry the user's id, email, roles and
/// linked EmployeeId. Mirrors the claim set produced by
/// <see cref="EmployeeClaimsPrincipalFactory"/> on the cookie side, so the
/// same <c>ICurrentUserService</c> implementation works for both pipelines.
/// </summary>
public interface IJwtTokenService
{
    Task<(string Token, DateTime ExpiresAtUtc)> GenerateAsync(ApplicationUser user, CancellationToken ct);
}

internal class JwtTokenService : IJwtTokenService
{
    public const string EmployeeIdClaim = EmployeeClaimsPrincipalFactory.EmployeeIdClaim;

    private readonly UserManager<ApplicationUser> _users;
    private readonly JwtOptions _options;

    public JwtTokenService(UserManager<ApplicationUser> users, IOptions<JwtOptions> options)
    {
        _users = users;
        _options = options.Value;
    }

    public async Task<(string Token, DateTime ExpiresAtUtc)> GenerateAsync(
        ApplicationUser user, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var expires = now.Add(_options.Lifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id),
            new(EmployeeIdClaim, user.EmployeeId.ToString())
        };

        if (!string.IsNullOrEmpty(user.Email))
            claims.Add(new Claim(ClaimTypes.Email, user.Email));

        foreach (var role in await _users.GetRolesAsync(user))
            claims.Add(new Claim(ClaimTypes.Role, role));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
