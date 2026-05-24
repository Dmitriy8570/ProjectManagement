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
}

public sealed class SearchEmployeesQueryHandler
    : IRequestHandler<SearchEmployeesQuery, IReadOnlyList<EmployeeDto>>
{
    private const int MaxLimit = 100;

    private readonly IEmployeeRepository _employeeRepository;

    public SearchEmployeesQueryHandler(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<IReadOnlyList<EmployeeDto>> Handle(SearchEmployeesQuery request, CancellationToken ct)
    {
        // Clamp to a safe range; callers can ask for less but never more than MaxLimit.
        var limit = Math.Clamp(request.Limit, 1, MaxLimit);

        var employees = await _employeeRepository.SearchEmployeesAsync(request.Term, limit, ct);
        return employees.Select(e => e.ToDto()).ToArray();
    }
}
