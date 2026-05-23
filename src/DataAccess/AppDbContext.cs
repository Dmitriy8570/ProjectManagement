using BusinessLogic.Documents;
using BusinessLogic.Employees;
using BusinessLogic.Projects;
using BusinessLogic.Tasks;
using DataAccess.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DataAccess;

public sealed class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectDocument> ProjectDocuments => Set<ProjectDocument>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Discover every IEntityTypeConfiguration in this assembly so adding
        // a new entity is just dropping a configuration file under
        // /Configurations — no extra wiring required.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Field-access on the skip-navigations of the Project ↔ Employee
        // many-to-many. Configured here (rather than in the per-entity
        // configurations) so it runs after the relationship has been defined,
        // independent of the order in which configurations were applied.
        modelBuilder.Entity<Project>()
            .Navigation(p => p.Employees)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        modelBuilder.Entity<Employee>()
            .Navigation(e => e.Projects)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
