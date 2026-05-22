using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Employees.Commands;
using NSubstitute;

namespace Tests.BusinessLogic.Employees.Commands;

public class CreateEmployeeCommandHandlerTests
{
    private readonly IEmployeeRepository _repository;
    private readonly CreateEmployeeCommandHandler _handler;

    public CreateEmployeeCommandHandlerTests()
    {
        _repository = Substitute.For<IEmployeeRepository>();
        _handler = new CreateEmployeeCommandHandler(_repository);
    }

    private static CreateEmployeeRequest CreateRequest(
        string firstName = "FirstName",
        string lastName = "LastName",
        string? patronymic = "Patronymic",
        string email = "email@example.com") =>
        new()
        {
            FirstName = firstName,
            LastName = lastName,
            Patronymic = patronymic,
            Email = email,
        };

    [Fact]
    public async Task Handle_ValidData_CreatesEmployeeAndReturnsId()
    {
        var request = CreateRequest();
        var command = new CreateEmployeeCommand { Data = request };

        _repository.EmailExistsAsync(request.Email, null, Arg.Any<CancellationToken>()).Returns(false);
        _repository
            .When(x => x.AddEmployeeAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>()))
            .Do(x => x.Arg<Employee>().GetType().GetProperty("Id")!.SetValue(x.Arg<Employee>(), 42));

        var response = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(42, response.Id);
        await _repository.Received(1).EmailExistsAsync(request.Email, null, Arg.Any<CancellationToken>());
        await _repository.Received(1).AddEmployeeAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsAndSkipsAddAndSave()
    {
        var request = CreateRequest();
        var command = new CreateEmployeeCommand { Data = request };

        _repository.EmailExistsAsync(request.Email, null, Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<DomainValidationException>(
            () => _handler.Handle(command, CancellationToken.None));

        await _repository.DidNotReceive().AddEmployeeAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    // Required fields (FirstName, LastName, Email) plus email syntax — Patronymic
    // is optional and intentionally excluded here.
    [Theory]
    [InlineData("",          "LastName", "email@example.com")]
    [InlineData("   ",       "LastName", "email@example.com")]
    [InlineData("FirstName", "",         "email@example.com")]
    [InlineData("FirstName", "LastName", "not-an-email")]
    public async Task Handle_InvalidRequiredFields_ThrowsDomainValidationException(
        string firstName, string lastName, string email)
    {
        var request = CreateRequest(firstName, lastName, "Patronymic", email);
        var command = new CreateEmployeeCommand { Data = request };

        await Assert.ThrowsAsync<DomainValidationException>(
            () => _handler.Handle(command, CancellationToken.None));

        await _repository.DidNotReceive().AddEmployeeAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    // Patronymic is optional: null/empty must be accepted by the handler and
    // the resulting Employee stored with an empty patronymic.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_BlankPatronymic_StoresEmployeeWithEmptyPatronymic(string? patronymic)
    {
        var request = CreateRequest(patronymic: patronymic);
        var command = new CreateEmployeeCommand { Data = request };

        _repository.EmailExistsAsync(request.Email, null, Arg.Any<CancellationToken>()).Returns(false);

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).AddEmployeeAsync(
            Arg.Is<Employee>(e => e.Patronymic == string.Empty),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidData_PassesCorrectEmployeeToRepository()
    {
        var request = CreateRequest("John", "Doe", "Patrick", "john@example.com");
        var command = new CreateEmployeeCommand { Data = request };

        _repository.EmailExistsAsync(request.Email, null, Arg.Any<CancellationToken>()).Returns(false);

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).AddEmployeeAsync(
            Arg.Is<Employee>(e =>
                e.FirstName == "John" &&
                e.LastName == "Doe" &&
                e.Patronymic == "Patrick" &&
                e.Email == "john@example.com"),
            Arg.Any<CancellationToken>());
    }
}
