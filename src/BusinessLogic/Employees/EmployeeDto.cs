namespace BusinessLogic.Employees;

public record EmployeeDto
{
    public int Id { get; init; }
    public string FirstName { get; init; } = default!;
    public string LastName { get; init; } = default!;
    public string Patronymic { get; init; } = default!;

    /// <summary>
    /// Sourced from the linked Identity user account (ApplicationUser.Email).
    /// Empty when the projection didn't request it (e.g. employees nested
    /// inside a ProjectDto — projects don't show employee emails).
    /// </summary>
    public string Email { get; init; } = string.Empty;

    public string FullName => $"{LastName} {FirstName} {Patronymic}".Trim();
}

/// <summary>
/// Hand-written entity → DTO projection for cases where Email isn't needed
/// (project-nested employees). Repository methods that need Email project
/// the DTO directly via a join — they don't go through this helper.
/// </summary>
internal static class EmployeeMapping
{
    public static EmployeeDto ToDto(this Employee employee) => new()
    {
        Id = employee.Id,
        FirstName = employee.FirstName,
        LastName = employee.LastName,
        Patronymic = employee.Patronymic
    };
}
