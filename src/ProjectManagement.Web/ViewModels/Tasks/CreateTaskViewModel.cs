using System.ComponentModel.DataAnnotations;
using BusinessLogic.Projects;
using BusinessLogic.Tasks;

namespace ProjectManagement.Web.ViewModels.Tasks;

public sealed class CreateTaskViewModel
{
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

    [Range(1, int.MaxValue, ErrorMessage = "Please select a project")]
    [Display(Name = "Project")]
    public int ProjectId { get; set; }

    // Allows the URL to lock the dropdown when reached via /projects/{id}/tasks/create.
    public bool ProjectLocked { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select an author")]
    [Display(Name = "Author")]
    public int AuthorId { get; set; }

    public string? AuthorName { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select an assignee")]
    [Display(Name = "Assignee")]
    public int AssigneeId { get; set; }

    // Populated server-side for the project selector and the assignee dropdown so
    // the view never has to call the DB itself.
    public IReadOnlyList<ProjectDto> AvailableProjects { get; set; } = [];
    public ProjectDto? SelectedProject { get; set; }
}
