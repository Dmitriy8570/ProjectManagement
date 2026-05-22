using System.ComponentModel.DataAnnotations;

namespace ProjectManagement.Web.ViewModels.Projects;

public class CreateProjectViewModel
{
    // Step 1: Basic info
    [Required(ErrorMessage = "Project name is required")]
    [MaxLength(200)]
    [Display(Name = "Project Name")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Start date is required")]
    [DataType(DataType.Date)]
    [Display(Name = "Start Date")]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "End date is required")]
    [DataType(DataType.Date)]
    [Display(Name = "End Date")]
    public DateTime EndDate { get; set; } = DateTime.Today.AddMonths(3);

    [Range(0, int.MaxValue, ErrorMessage = "Priority must be 0 or greater")]
    [Display(Name = "Priority")]
    public int Priority { get; set; }

    // Step 2: Companies
    [Required(ErrorMessage = "Customer company is required")]
    [MaxLength(200)]
    [Display(Name = "Customer Company")]
    public string CustomerCompany { get; set; } = "";

    [Required(ErrorMessage = "Executing company is required")]
    [MaxLength(200)]
    [Display(Name = "Executing Company")]
    public string ExecutingCompany { get; set; } = "";

    // Step 3: Project manager (AJAX autocomplete)
    [Range(1, int.MaxValue, ErrorMessage = "Please select a project manager")]
    [Display(Name = "Project Manager")]
    public int ProjectManagerId { get; set; }

    public string? ProjectManagerName { get; set; }

    // Step 4: Team members (AJAX multi-autocomplete)
    public List<int> EmployeeIds { get; set; } = [];

    // Display names for error-recovery (re-populate chips after failed submit)
    public List<string> EmployeeNames { get; set; } = [];

    // Step 5: File upload — placeholder for future implementation
    // public IFormFileCollection? Documents { get; set; }
}
