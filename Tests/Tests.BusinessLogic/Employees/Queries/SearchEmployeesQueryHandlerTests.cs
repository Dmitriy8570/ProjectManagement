using BusinessLogic.Employees;
using BusinessLogic.Employees.Queries;
using NSubstitute;

namespace Tests.BusinessLogic.Employees.Queries;

public class SearchEmployeesQueryHandlerTests
{
    private readonly IEmployeeRepository _repository;
    private readonly SearchEmployeesQueryHandler _handler;

    public SearchEmployeesQueryHandlerTests()
    {
        _repository = Substitute.For<IEmployeeRepository>();
        _handler = new SearchEmployeesQueryHandler(_repository);
    }

    // Repository now projects EmployeeDto via a join to AspNetUsers, so the
    // handler just passes the result straight through. Tests focus on the
    // handler's two responsibilities: clamping `Limit` and forwarding the
    // role filter to the repository.

    private static EmployeeDto CreateDto(
        int id = 1,
        string firstName = "John",
        string lastName = "Doe",
        string patronymic = "Jr",
        string email = "john@example.com") =>
        new()
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            Patronymic = patronymic,
            Email = email,
        };

    [Fact]
    public async Task Handle_WithTerm_PassesTermAndReturnsDto()
    {
        var dto = CreateDto(id: 7, firstName: "Alice", lastName: "Smith", patronymic: "K", email: "alice@example.com");
        _repository.SearchEmployeesAsync("alice", 20, null, Arg.Any<CancellationToken>())
            .Returns(new[] { dto });
        var query = new SearchEmployeesQuery { Term = "alice", Limit = 20 };

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(7, result[0].Id);
        Assert.Equal("Alice", result[0].FirstName);
        Assert.Equal("Smith", result[0].LastName);
        Assert.Equal("K", result[0].Patronymic);
        Assert.Equal("alice@example.com", result[0].Email);
    }

    [Fact]
    public async Task Handle_NullTerm_PassesNullToRepository()
    {
        _repository.SearchEmployeesAsync(null, Arg.Any<int>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<EmployeeDto>());
        var query = new SearchEmployeesQuery { Term = null, Limit = 20 };

        await _handler.Handle(query, CancellationToken.None);

        await _repository.Received(1).SearchEmployeesAsync(null, 20, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmptyResult_ReturnsEmptyList()
    {
        _repository.SearchEmployeesAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<EmployeeDto>());
        var query = new SearchEmployeesQuery { Term = "unknown", Limit = 20 };

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_LimitAboveMax_ClampsTo100()
    {
        _repository.SearchEmployeesAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<EmployeeDto>());
        var query = new SearchEmployeesQuery { Term = null, Limit = 500 };

        await _handler.Handle(query, CancellationToken.None);

        await _repository.Received(1).SearchEmployeesAsync(Arg.Any<string?>(), 100, Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Handle_LimitBelowMin_ClampsTo1(int limit)
    {
        _repository.SearchEmployeesAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<EmployeeDto>());
        var query = new SearchEmployeesQuery { Term = null, Limit = limit };

        await _handler.Handle(query, CancellationToken.None);

        await _repository.Received(1).SearchEmployeesAsync(Arg.Any<string?>(), 1, Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidLimit_PassesUnchanged()
    {
        _repository.SearchEmployeesAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<EmployeeDto>());
        var query = new SearchEmployeesQuery { Term = "x", Limit = 50 };

        await _handler.Handle(query, CancellationToken.None);

        await _repository.Received(1).SearchEmployeesAsync("x", 50, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RolesFilter_ForwardedToRepository()
    {
        _repository.SearchEmployeesAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<EmployeeDto>());
        var roles = new[] { "Director", "ProjectManager" };
        var query = new SearchEmployeesQuery { Term = "x", Limit = 10, Roles = roles };

        await _handler.Handle(query, CancellationToken.None);

        await _repository.Received(1).SearchEmployeesAsync(
            "x", 10,
            Arg.Is<IReadOnlyList<string>?>(r => r != null && r.SequenceEqual(roles)),
            Arg.Any<CancellationToken>());
    }
}
