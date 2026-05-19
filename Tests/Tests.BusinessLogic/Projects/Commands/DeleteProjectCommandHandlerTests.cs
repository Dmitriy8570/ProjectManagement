using BusinessLogic.Common;
using BusinessLogic.Projects;
using BusinessLogic.Projects.Commands;
using NSubstitute;

namespace Tests.BusinessLogic.Projects.Commands;

public class DeleteProjectCommandHandlerTests
{
    private readonly IProjectRepository _repository;
    private readonly DeleteProjectCommandHandler _handler;

    public DeleteProjectCommandHandlerTests()
    {
        _repository = Substitute.For<IProjectRepository>();
        _handler = new DeleteProjectCommandHandler(_repository);
    }

    [Fact]
    public async Task Handle_ValidDelete_DeletesWithoutCallingSave()
    {
        _repository.DeleteProjectAsync(1, Arg.Any<CancellationToken>()).Returns(true);
        var command = new DeleteProjectCommand { Id = 1 };

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).DeleteProjectAsync(1, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ProjectNotFound_ThrowsEntityNotFoundException()
    {
        _repository.DeleteProjectAsync(99, Arg.Any<CancellationToken>()).Returns(false);
        var command = new DeleteProjectCommand { Id = 99 };

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
