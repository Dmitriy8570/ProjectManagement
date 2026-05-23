namespace BusinessLogic.Identity;

/// <summary>
/// String constants for the three roles defined by the spec. Kept as constants
/// (not an enum) so they can flow directly into <c>[Authorize(Roles = ...)]</c>
/// attributes, which expect comma-separated strings.
/// </summary>
public static class Roles
{
    /// <summary>Руководитель — full access to every page and entity.</summary>
    public const string Director = "Director";

    /// <summary>
    /// Менеджер проекта — manages assigned projects and tasks on them; can
    /// add/remove participants on their projects but cannot create new
    /// employee accounts.
    /// </summary>
    public const string ProjectManager = "ProjectManager";

    /// <summary>Сотрудник — sees own projects/tasks, can change own task status.</summary>
    public const string Employee = "Employee";

    /// <summary>All three roles, useful for <c>[Authorize(Roles = Roles.All)]</c>.</summary>
    public const string All = Director + "," + ProjectManager + "," + Employee;

    public static readonly IReadOnlyList<string> AllList = new[] { Director, ProjectManager, Employee };
}
