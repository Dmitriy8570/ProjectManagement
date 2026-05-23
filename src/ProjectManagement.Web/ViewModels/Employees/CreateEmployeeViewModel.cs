using System.ComponentModel.DataAnnotations;
using BusinessLogic.Identity;

namespace ProjectManagement.Web.ViewModels.Employees;

/// <summary>
/// Used only on the Create form — extends the shared form with the fields
/// that are mandatory on registration but irrelevant on edit (password
/// and initial role). Edits don't change either of these here; password
/// is reset through Identity's own flow, role through a separate admin
/// screen (future work).
/// </summary>
public sealed class CreateEmployeeViewModel : EmployeeFormViewModel
{
    [Required, DataType(DataType.Password), MinLength(6), MaxLength(100)]
    [Display(Name = "Password")]
    public string Password { get; set; } = "";

    [Required]
    [Display(Name = "Role")]
    public string Role { get; set; } = Roles.Employee;
}
