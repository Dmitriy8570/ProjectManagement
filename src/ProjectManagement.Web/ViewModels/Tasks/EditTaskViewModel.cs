using System.ComponentModel.DataAnnotations;
using BusinessLogic.Projects;
using BusinessLogic.Tasks;

namespace ProjectManagement.Web.ViewModels.Tasks;

public class EditTaskViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Task name is required")]
    [MaxLength(200)]
    [Display(Name = "Task Name")]
    public string Name { get; set; } = "";

    [MaxLength(2000)]
    [Display(Name = "Comment")]
    public string? Comment { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Priority must be 0 or greater")]
    [Display(Name = "Priority")]
    public int Priority { get; set; }

    [Display(Name = "Status")]
    public ProjectTaskStatus Status { get; set; } = ProjectTaskStatus.ToDo;

    [Range(1, int.MaxValue, ErrorMessage = "Please select an assignee")]
    [Display(Name = "Assignee")]
    public int AssigneeId { get; set; }

    // Display-only — the project of a task cannot be changed once created.
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = "";
    public ProjectDto? Project { get; set; }
}
