using System.ComponentModel.DataAnnotations;

namespace ProjectManagement.Web.ViewModels.Projects;

public sealed class EditProjectViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Project name is required")]
    [MaxLength(200)]
    [Display(Name = "Project Name")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Start date is required")]
    [DataType(DataType.Date)]
    [Display(Name = "Start Date")]
    public DateTime StartDate { get; set; }

    [Required(ErrorMessage = "End date is required")]
    [DataType(DataType.Date)]
    [Display(Name = "End Date")]
    public DateTime EndDate { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Priority must be 0 or greater")]
    [Display(Name = "Priority")]
    public int Priority { get; set; }

    [Required(ErrorMessage = "Customer company is required")]
    [MaxLength(200)]
    [Display(Name = "Customer Company")]
    public string CustomerCompany { get; set; } = "";

    [Required(ErrorMessage = "Executing company is required")]
    [MaxLength(200)]
    [Display(Name = "Executing Company")]
    public string ExecutingCompany { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "Please select a project manager")]
    [Display(Name = "Project Manager")]
    public int ProjectManagerId { get; set; }

    // Pre-populated from the loaded project for the autocomplete display
    public string? ProjectManagerName { get; set; }
}
