using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Employees.Queries;
using NSubstitute;

namespace Tests.BusinessLogic.Employees.Queries;

public class GetEmployeeByIdQueryHandlerTests
{
    private readonly IEmployeeRepository _repository;
    private readonly GetEmployeeByIdQueryHandler _handler;

    public GetEmployeeByIdQueryHandlerTests()
    {
        _repository = Substitute.For<IEmployeeRepository>();
        _handler = new GetEmployeeByIdQueryHandler(_repository);
    }

    private static Employee CreateEmployee(
        int id = 1,
        string firstName = "John",
        string lastName = "Doe",
        string patronymic = "Jr",
        string email = "john@example.com")
    {
        var employee = new Employee(firstName, lastName, patronymic, email);
        typeof(Employee).GetProperty("Id")!.SetValue(employee, id);
        return employee;
    }

    [Fact]
    public async Task Handle_EmployeeFound_ReturnsMappedDto()
    {
        var employee = CreateEmployee(id: 3, firstName: "Alice", lastName: "Smith", patronymic: "K", email: "alice@example.com");
        _repository.GetEmployeeByIdAsync(3, Arg.Any<CancellationToken>()).Returns(employee);
        var query = new GetEmployeeByIdQuery { Id = 3 };

        var dto = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(3, dto.Id);
        Assert.Equal("Alice", dto.FirstName);
        Assert.Equal("Smith", dto.LastName);
        Assert.Equal("K", dto.Patronymic);
        Assert.Equal("alice@example.com", dto.Email);
    }

    [Fact]
    public async Task Handle_EmployeeNotFound_ThrowsEntityNotFoundException()
    {
        _repository.GetEmployeeByIdAsync(99, Arg.Any<CancellationToken>()).Returns((Employee?)null);
        var query = new GetEmployeeByIdQuery { Id = 99 };

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(query, CancellationToken.None));
    }
}
