namespace ProjectManagement.Api.Dtos.Auth;

public record LoginResponse
{
    /// <summary>HS256-signed JWT. Send back as <c>Authorization: Bearer &lt;token&gt;</c>.</summary>
    public string Token { get; init; } = "";

    /// <summary>UTC instant after which the token will no longer validate.</summary>
    public DateTime ExpiresAtUtc { get; init; }

    public CurrentUserDto User { get; init; } = default!;
}

public record CurrentUserDto
{
    public string Id { get; init; } = "";
    public string Email { get; init; } = "";
    public int EmployeeId { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
}
