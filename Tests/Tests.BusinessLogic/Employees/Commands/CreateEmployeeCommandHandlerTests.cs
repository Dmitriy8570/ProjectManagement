using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Employees.Commands;
using BusinessLogic.Identity;
using NSubstitute;

namespace Tests.BusinessLogic.Employees.Commands;

public class CreateEmployeeCommandHandlerTests
{
    private readonly IEmployeeRepository _repository;
    private readonly IUserAccountService _accounts;
    private readonly CreateEmployeeCommandHandler _handler;

    public CreateEmployeeCommandHandlerTests()
    {
        _repository = Substitute.For<IEmployeeRepository>();
        _accounts = Substitute.For<IUserAccountService>();
        _handler = new CreateEmployeeCommandHandler(_repository, _accounts);
    }

    private static CreateEmployeeRequest CreateRequest(
        string firstName = "FirstName",
        string lastName = "LastName",
        string? patronymic = "Patronymic",
        string email = "email@example.com",
        string password = "Aa1!aaaa",
        string? role = Roles.Employee) =>
        new()
        {
            FirstName = firstName,
            LastName = lastName,
            Patronymic = patronymic,
            Email = email,
            Password = password,
            Role = role,
        };

    [Fact]
    public async Task Handle_ValidData_CreatesEmployeeAndAccountAndReturnsId()
    {
        var request = CreateRequest();
        var command = new CreateEmployeeCommand { Data = request };

        _accounts.EmailExistsAsync(request.Email, null, Arg.Any<CancellationToken>()).Returns(false);
        _repository
            .When(x => x.AddEmployeeAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>()))
            .Do(x => x.Arg<Employee>().GetType().GetProperty("Id")!.SetValue(x.Arg<Employee>(), 42));

        var response = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(42, response.Id);
        await _accounts.Received(1).EmailExistsAsync(request.Email, null, Arg.Any<CancellationToken>());
        await _accounts.Received(1).ValidateNewAccountAsync(request.Email, request.Password, Arg.Any<CancellationToken>());
        await _repository.Received(1).AddEmployeeAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveAsync(Arg.Any<CancellationToken>());
        await _accounts.Received(1).CreateAccountAsync(42, request.Email, request.Password, request.Role, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsAndSkipsAccountCreation()
    {
        var request = CreateRequest();
        var command = new CreateEmployeeCommand { Data = request };

        _accounts.EmailExistsAsync(request.Email, null, Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<DomainValidationException>(
            () => _handler.Handle(command, CancellationToken.None));

        // Bail-out happens before validation, persistence, or account creation.
        await _accounts.DidNotReceive().ValidateNewAccountAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AddEmployeeAsync(
            Arg.Any<Employee>(), Arg.Any<CancellationToken>());
        await _accounts.DidNotReceive().CreateAccountAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WeakPassword_ThrowsBeforePersistingEmployee()
    {
        var request = CreateRequest(password: "weak");
        var command = new CreateEmployeeCommand { Data = request };

        _accounts.EmailExistsAsync(request.Email, null, Arg.Any<CancellationToken>()).Returns(false);
        _accounts
            .When(x => x.ValidateNewAccountAsync(request.Email, request.Password, Arg.Any<CancellationToken>()))
            .Do(_ => throw new DomainValidationException("Password too short."));

        await Assert.ThrowsAsync<DomainValidationException>(
            () => _handler.Handle(command, CancellationToken.None));

        // The orphan-Employee bug we previously had: validation must run BEFORE
        // AddEmployeeAsync, otherwise a weak password leaves a stray employee.
        await _repository.DidNotReceive().AddEmployeeAsync(
            Arg.Any<Employee>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CreateAccountFails_DeletesOrphanEmployeeAndRethrows()
    {
        var request = CreateRequest();
        var command = new CreateEmployeeCommand { Data = request };

        _accounts.EmailExistsAsync(request.Email, null, Arg.Any<CancellationToken>()).Returns(false);
        _repository
            .When(x => x.AddEmployeeAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>()))
            .Do(x => x.Arg<Employee>().GetType().GetProperty("Id")!.SetValue(x.Arg<Employee>(), 77));
        _accounts
            .When(x => x.CreateAccountAsync(
                77, request.Email, request.Password, request.Role, Arg.Any<CancellationToken>()))
            .Do(_ => throw new DomainValidationException("Race: email taken."));

        await Assert.ThrowsAsync<DomainValidationException>(
            () => _handler.Handle(command, CancellationToken.None));

        // Safety net: the just-inserted employee row must be rolled back so the
        // table stays consistent — orphan rows are the bug this guard prevents.
        await _repository.Received(1).DeleteEmployeeAsync(77, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("",          "LastName")]
    [InlineData("   ",       "LastName")]
    [InlineData("FirstName", "")]
    public async Task Handle_BlankNameField_ThrowsDomainValidationException(
        string firstName, string lastName)
    {
        var request = CreateRequest(firstName: firstName, lastName: lastName);
        var command = new CreateEmployeeCommand { Data = request };

        _accounts.EmailExistsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<DomainValidationException>(
            () => _handler.Handle(command, CancellationToken.None));

        await _repository.DidNotReceive().AddEmployeeAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_BlankPatronymic_StoresEmployeeWithEmptyPatronymic(string? patronymic)
    {
        var request = CreateRequest(patronymic: patronymic);
        var command = new CreateEmployeeCommand { Data = request };

        _accounts.EmailExistsAsync(request.Email, null, Arg.Any<CancellationToken>()).Returns(false);

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

        _accounts.EmailExistsAsync(request.Email, null, Arg.Any<CancellationToken>()).Returns(false);

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).AddEmployeeAsync(
            Arg.Is<Employee>(e =>
                e.FirstName == "John" &&
                e.LastName == "Doe" &&
                e.Patronymic == "Patrick"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownRole_ThrowsDomainValidationException()
    {
        var request = CreateRequest(role: "Hacker");
        var command = new CreateEmployeeCommand { Data = request };

        _accounts.EmailExistsAsync(request.Email, null, Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<DomainValidationException>(
            () => _handler.Handle(command, CancellationToken.None));

        await _repository.DidNotReceive().AddEmployeeAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NullRole_DefaultsToEmployeeRole()
    {
        var request = CreateRequest(role: null);
        var command = new CreateEmployeeCommand { Data = request };

        _accounts.EmailExistsAsync(request.Email, null, Arg.Any<CancellationToken>()).Returns(false);
        _repository
            .When(x => x.AddEmployeeAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>()))
            .Do(x => x.Arg<Employee>().GetType().GetProperty("Id")!.SetValue(x.Arg<Employee>(), 9));

        await _handler.Handle(command, CancellationToken.None);

        await _accounts.Received(1).CreateAccountAsync(
            9, request.Email, request.Password, Roles.Employee, Arg.Any<CancellationToken>());
    }
}
