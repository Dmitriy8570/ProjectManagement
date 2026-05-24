using System.ComponentModel.DataAnnotations;

namespace ProjectManagement.Api.Dtos.Auth;

public sealed class LoginRequest
{
    [Required, EmailAddress, MaxLength(100)]
    public string Email { get; set; } = "";

    [Required, MaxLength(100)]
    public string Password { get; set; } = "";
}
