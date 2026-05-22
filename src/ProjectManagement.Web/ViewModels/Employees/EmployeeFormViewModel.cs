using System.ComponentModel.DataAnnotations;

namespace ProjectManagement.Web.ViewModels.Employees;

public class EmployeeFormViewModel
{
    [Required(ErrorMessage = "Last name is required")]
    [MaxLength(100)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = "";

    [Required(ErrorMessage = "First name is required")]
    [MaxLength(100)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = "";

    [MaxLength(100)]
    [Display(Name = "Patronymic")]
    public string? Patronymic { get; set; }

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Enter a valid email address")]
    [MaxLength(100)]
    [Display(Name = "Email")]
    public string Email { get; set; } = "";
}
