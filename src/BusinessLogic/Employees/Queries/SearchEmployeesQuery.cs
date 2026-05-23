using MediatR;

namespace BusinessLogic.Employees.Queries;

/// <summary>
/// Drives the wizard's autocomplete dropdown. The frontend fires this on each
/// keystroke (debounced); we cap the result count so a wide-open query can't
/// hammer the database — the user is going to pick from the first few hits
/// either way.
/// </summary>
public record SearchEmployeesQuery : IRequest<IReadOnlyList<EmployeeDto>>
{
    public string? Term { get; init; }
    public int Limit { get; init; } = 20;

    /// <summary>
    /// Optional whitelist of Identity roles — when set, only employees whose
    /// linked account is in at least one of these roles are returned. Used
    /// by the PM picker on the project wizard to hide plain Сотрудник users.
    /// </summary>
    public IReadOnlyList<string>? Roles { get; init; }
}

public class SearchEmployeesQueryHandler
    : IRequestHandler<SearchEmployeesQuery, IReadOnlyList<EmployeeDto>>
{
    private const int MaxLimit = 100;

    private readonly IEmployeeRepository _employeeRepository;

    public SearchEmployeesQueryHandler(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public Task<IReadOnlyList<EmployeeDto>> Handle(SearchEmployeesQuery request, CancellationToken ct)
    {
        // Clamp to a safe range; callers can ask for less but never more than MaxLimit.
        var limit = Math.Clamp(request.Limit, 1, MaxLimit);

        return _employeeRepository.SearchEmployeesAsync(request.Term, limit, request.Roles, ct);
    }
}
