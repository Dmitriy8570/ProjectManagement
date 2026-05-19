using BusinessLogic.Employees;
using BusinessLogic.Projects;
using DataAccess.Repositories;

namespace Tests.DataAccess;

public class  ProjectRepositoryTests : DatabaseTestBase
{
    [Fact]
    public async Task GetProjectsAsync_FiltersByPriorityRange()
    {
        var pm = new Employee("PM", "PM", "PM", "pm@x.com");
        Db.Employees.Add(pm);
        await Db.SaveChangesAsync();

        Db.Projects.AddRange(
            new Project("Low", "c", "e", new(2024, 1, 1), new(2024, 12, 31), pm, 1),
            new Project("Mid", "c", "e", new(2024, 1, 1), new(2024, 12, 31), pm, 5),
            new Project("High", "c", "e", new(2024, 1, 1), new(2024, 12, 31), pm, 9));
        await Db.SaveChangesAsync(default);

        var sut = new ProjectRepository(Db);

        var page = await sut.GetProjectsAsync(
            new ProjectListFilter { MinPriority = 3, MaxPriority = 7 }, default);

        Assert.Single(page.Items);
        Assert.Equal("Mid", page.Items[0].Name);
    }
}