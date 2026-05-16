namespace BusinessLogic.Employees;

public record EmployeeDto
{
    public int Id { get; init; }
    public string FirstName { get; init; } = default!;
    public string LastName { get; init; } = default!;
    public string Patronymic { get; init; } = default!;
    public string Email { get; init; } = default!;

    public string FullName => $"{LastName} {FirstName} {Patronymic}".Trim();
}

/// <summary>
/// Hand-written entity → DTO projection. Lightweight enough that pulling in
/// AutoMapper would cost more than it would save.
/// </summary>
internal static class EmployeeMapping
{
    public static EmployeeDto ToDto(this Employee employee) => new()
    {
        Id = employee.Id,
        FirstName = employee.FirstName,
        LastName = employee.LastName,
        Patronymic = employee.Patronymic,
        Email = employee.Email
    };
}
