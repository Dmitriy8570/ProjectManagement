using BusinessLogic.Common;
using MediatR;

namespace BusinessLogic.Employees.Queries;

public record GetEmployeeByIdQuery : IRequest<EmployeeDto>
{
    public int Id { get; init; }
}

public class GetEmployeeByIdQueryHandler : IRequestHandler<GetEmployeeByIdQuery, EmployeeDto>
{
    private readonly IEmployeeRepository _employeeRepository;

    public GetEmployeeByIdQueryHandler(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<EmployeeDto> Handle(GetEmployeeByIdQuery request, CancellationToken ct)
    {
        return await _employeeRepository.GetEmployeeDtoByIdAsync(request.Id, ct)
            ?? throw new EntityNotFoundException(nameof(Employee), request.Id);
    }
}
