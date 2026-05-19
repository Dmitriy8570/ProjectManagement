using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Employees.Commands;
using NSubstitute;

namespace Tests.BusinessLogic.Employees.Commands;

public class DeleteEmployeeCommandHandlerTests
{
    private readonly IEmployeeRepository _repository;
    private readonly DeleteEmployeeCommandHandler _handler;

    public DeleteEmployeeCommandHandlerTests()
    {
        _repository = Substitute.For<IEmployeeRepository>();
        _handler = new DeleteEmployeeCommandHandler(_repository);
    }

    [Fact]
    public async Task Handle_EmployeeIsProjectManager_ThrowsAndSkipsDelete()
    {
        _repository.IsProjectManagerAsync(1, Arg.Any<CancellationToken>()).Returns(true);
        var command = new DeleteEmployeeCommand { Id = 1 };

        await Assert.ThrowsAsync<DomainValidationException>(
            () => _handler.Handle(command, CancellationToken.None));

        await _repository.DidNotReceive().DeleteEmployeeAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmployeeNotFound_ThrowsEntityNotFoundException()
    {
        _repository.IsProjectManagerAsync(99, Arg.Any<CancellationToken>()).Returns(false);
        _repository.DeleteEmployeeAsync(99, Arg.Any<CancellationToken>()).Returns(false);
        var command = new DeleteEmployeeCommand { Id = 99 };

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidDelete_DeletesWithoutCallingSave()
    {
        _repository.IsProjectManagerAsync(1, Arg.Any<CancellationToken>()).Returns(false);
        _repository.DeleteEmployeeAsync(1, Arg.Any<CancellationToken>()).Returns(true);
        var command = new DeleteEmployeeCommand { Id = 1 };

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).IsProjectManagerAsync(1, Arg.Any<CancellationToken>());
        await _repository.Received(1).DeleteEmployeeAsync(1, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }
}
