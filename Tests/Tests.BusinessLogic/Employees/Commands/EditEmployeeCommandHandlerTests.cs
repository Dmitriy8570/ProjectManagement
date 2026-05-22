using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Employees.Commands;
using NSubstitute;

namespace Tests.BusinessLogic.Employees.Commands;

public class EditEmployeeCommandHandlerTests
{
    private readonly IEmployeeRepository _repository;
    private readonly EditEmployeeCommandHandler _handler;

    public EditEmployeeCommandHandlerTests()
    {
        _repository = Substitute.For<IEmployeeRepository>();
        _handler = new EditEmployeeCommandHandler(_repository);
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
    public async Task Handle_EmployeeNotFound_ThrowsEntityNotFoundException()
    {
        _repository.GetEmployeeByIdAsync(99, Arg.Any<CancellationToken>()).Returns((Employee?)null);
        var command = new EditEmployeeCommand { Id = 99, Data = new EditEmployeeRequest() };

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));

        await _repository.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidUpdate_MutatesEmployeeAndSaves()
    {
        var employee = CreateEmployee(id: 5);
        _repository.GetEmployeeByIdAsync(5, Arg.Any<CancellationToken>()).Returns(employee);
        var command = new EditEmployeeCommand
        {
            Id = 5,
            Data = new EditEmployeeRequest { FirstName = "Jane", LastName = "Smith" }
        };

        var response = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(5, response.Id);
        Assert.Equal("Jane", employee.FirstName);
        Assert.Equal("Smith", employee.LastName);
        await _repository.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AllNullFields_StillCallsSave()
    {
        var employee = CreateEmployee(id: 3);
        _repository.GetEmployeeByIdAsync(3, Arg.Any<CancellationToken>()).Returns(employee);
        var command = new EditEmployeeCommand { Id = 3, Data = new EditEmployeeRequest() };

        var response = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(3, response.Id);
        await _repository.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NewUniqueEmail_ChecksDuplicateWithExcludingId()
    {
        var employee = CreateEmployee(id: 5, email: "old@example.com");
        _repository.GetEmployeeByIdAsync(5, Arg.Any<CancellationToken>()).Returns(employee);
        _repository.EmailExistsAsync("new@example.com", 5, Arg.Any<CancellationToken>()).Returns(false);
        var command = new EditEmployeeCommand
        {
            Id = 5,
            Data = new EditEmployeeRequest { Email = "new@example.com" }
        };

        var response = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(5, response.Id);
        await _repository.Received(1).EmailExistsAsync("new@example.com", 5, Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SameEmailCaseInsensitive_SkipsDuplicateCheck()
    {
        var employee = CreateEmployee(id: 5, email: "JOHN@example.com");
        _repository.GetEmployeeByIdAsync(5, Arg.Any<CancellationToken>()).Returns(employee);
        var command = new EditEmployeeCommand
        {
            Id = 5,
            Data = new EditEmployeeRequest { Email = "john@example.com" }
        };

        await _handler.Handle(command, CancellationToken.None);

        await _repository.DidNotReceive()
            .EmailExistsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsAndSkipsSave()
    {
        var employee = CreateEmployee(id: 5, email: "old@example.com");
        _repository.GetEmployeeByIdAsync(5, Arg.Any<CancellationToken>()).Returns(employee);
        _repository.EmailExistsAsync("taken@example.com", 5, Arg.Any<CancellationToken>()).Returns(true);
        var command = new EditEmployeeCommand
        {
            Id = 5,
            Data = new EditEmployeeRequest { Email = "taken@example.com" }
        };

        await Assert.ThrowsAsync<DomainValidationException>(
            () => _handler.Handle(command, CancellationToken.None));

        await _repository.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    // Blank values for FirstName/LastName violate the NotBlank guard; an invalid
    // Email fails the email guard. Patronymic is optional and therefore not part
    // of this theory — see Handle_BlankPatronymic_ClearsPatronymicAndSaves below.
    [Theory]
    [InlineData("",    null, null)]
    [InlineData("   ", null, null)]
    [InlineData(null,  "",   null)]
    [InlineData(null,  null, "not-an-email")]
    public async Task Handle_InvalidFieldValue_ThrowsDomainValidationException(
        string? firstName, string? lastName, string? email)
    {
        var employee = CreateEmployee(id: 5);
        _repository.GetEmployeeByIdAsync(5, Arg.Any<CancellationToken>()).Returns(employee);
        var command = new EditEmployeeCommand
        {
            Id = 5,
            Data = new EditEmployeeRequest
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email
            }
        };

        await Assert.ThrowsAsync<DomainValidationException>(
            () => _handler.Handle(command, CancellationToken.None));

        await _repository.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    // Patronymic is optional: passing an empty/whitespace string is the
    // intended way to clear it, so the handler must save without throwing.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_BlankPatronymic_ClearsPatronymicAndSaves(string blank)
    {
        var employee = CreateEmployee(id: 5, patronymic: "Old");
        _repository.GetEmployeeByIdAsync(5, Arg.Any<CancellationToken>()).Returns(employee);

        var command = new EditEmployeeCommand
        {
            Id = 5,
            Data = new EditEmployeeRequest { Patronymic = blank }
        };

        await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(string.Empty, employee.Patronymic);
        await _repository.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }
}
