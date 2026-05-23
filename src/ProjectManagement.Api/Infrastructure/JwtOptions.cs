namespace ProjectManagement.Api.Infrastructure;

/// <summary>
/// Bound from the <c>Jwt</c> section of configuration. Lifetime is expressed
/// in hours to keep appsettings.json human-readable; the rest of the code
/// turns it into a <see cref="TimeSpan"/>.
/// </summary>
public sealed class JwtOptions
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";

    /// <summary>HS256 signing key. Must be at least 32 bytes (256 bits).</summary>
    public string Key { get; set; } = "";

    public int LifetimeHours { get; set; } = 8;

    public TimeSpan Lifetime => TimeSpan.FromHours(LifetimeHours);
}
