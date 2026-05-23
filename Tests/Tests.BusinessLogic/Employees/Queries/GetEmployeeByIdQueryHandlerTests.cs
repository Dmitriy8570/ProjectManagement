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

    [Fact]
    public async Task Handle_EmployeeFound_ReturnsDto()
    {
        var dto = new EmployeeDto
        {
            Id = 3,
            FirstName = "Alice",
            LastName = "Smith",
            Patronymic = "K",
            Email = "alice@example.com",
        };
        _repository.GetEmployeeDtoByIdAsync(3, Arg.Any<CancellationToken>()).Returns(dto);
        var query = new GetEmployeeByIdQuery { Id = 3 };

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(3, result.Id);
        Assert.Equal("Alice", result.FirstName);
        Assert.Equal("Smith", result.LastName);
        Assert.Equal("K", result.Patronymic);
        Assert.Equal("alice@example.com", result.Email);
    }

    [Fact]
    public async Task Handle_EmployeeNotFound_ThrowsEntityNotFoundException()
    {
        _repository.GetEmployeeDtoByIdAsync(99, Arg.Any<CancellationToken>()).Returns((EmployeeDto?)null);
        var query = new GetEmployeeByIdQuery { Id = 99 };

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(query, CancellationToken.None));
    }
}
