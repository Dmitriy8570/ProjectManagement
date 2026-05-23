using BusinessLogic.Common;
using BusinessLogic.Tasks;
using BusinessLogic.Tasks.Commands;
using NSubstitute;

namespace Tests.BusinessLogic.Tasks.Commands;

public class DeleteProjectTaskCommandHandlerTests
{
    private readonly IProjectTaskRepository _taskRepo;
    private readonly DeleteProjectTaskCommandHandler _handler;

    public DeleteProjectTaskCommandHandlerTests()
    {
        _taskRepo = Substitute.For<IProjectTaskRepository>();
        _handler = new DeleteProjectTaskCommandHandler(_taskRepo);
    }

    [Fact]
    public async Task Handle_ExistingTask_DeletesSuccessfully()
    {
        _taskRepo.DeleteTaskAsync(42, Arg.Any<CancellationToken>()).Returns(true);

        var command = new DeleteProjectTaskCommand { Id = 42 };

        await _handler.Handle(command, CancellationToken.None);

        await _taskRepo.Received(1).DeleteTaskAsync(42, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonExistentTask_ThrowsEntityNotFoundException()
    {
        _taskRepo.DeleteTaskAsync(99, Arg.Any<CancellationToken>()).Returns(false);

        var command = new DeleteProjectTaskCommand { Id = 99 };

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
