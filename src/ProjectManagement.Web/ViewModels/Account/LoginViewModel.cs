using System.ComponentModel.DataAnnotations;

namespace ProjectManagement.Web.ViewModels.Account;

public class LoginViewModel
{
    [Required, EmailAddress, MaxLength(100)]
    [Display(Name = "Email")]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password), MaxLength(100)]
    [Display(Name = "Password")]
    public string Password { get; set; } = "";

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    /// <summary>
    /// Populated from the query string when the user was redirected to the
    /// login page by the authentication middleware. The controller validates
    /// the URL is local before redirecting back, so this is safe to round-trip.
    /// </summary>
    public string? ReturnUrl { get; set; }
}
