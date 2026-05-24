using BusinessLogic.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.CustomerCompany)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.ExecutingCompany)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasMany(x => x.Employees)
            .WithMany(x => x.Projects)
            .UsingEntity(j => j.ToTable("ProjectEmployees"));

        // Restrict deletion of an employee while they manage a project — the
        // BusinessLogic.Employees.DeleteEmployeeCommand handler pre-checks
        // this and surfaces a domain error before EF Core ever runs the
        // DELETE statement.
        builder.HasOne(x => x.ProjectManager)
            .WithMany()
            .HasForeignKey(x => x.ProjectManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes on the filter / sort columns so the list endpoint scales
        // beyond a handful of rows. None of these are uniqueness constraints.
        builder.HasIndex(x => x.StartDate);
        builder.HasIndex(x => x.EndDate);
        builder.HasIndex(x => x.Priority);
        builder.HasIndex(x => x.ProjectManagerId);
    }
}
