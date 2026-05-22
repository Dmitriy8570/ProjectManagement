using BusinessLogic.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Configurations;

public class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> builder)
    {
        builder.ToTable("ProjectTasks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Comment)
            .HasMaxLength(2000)
            .IsRequired();

        // Status is stored as a string column so the database stays readable
        // and the enum can be reordered safely without rewriting historical rows.
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Cascade: deleting a project removes its tasks.
        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict on author / assignee — the BusinessLogic.Employees
        // DeleteEmployeeCommand handler pre-checks task references and surfaces
        // a domain error before the SQL constraint fires.
        builder.HasOne(x => x.Author)
            .WithMany()
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Assignee)
            .WithMany()
            .HasForeignKey(x => x.AssigneeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes on the filter / sort columns so list endpoints scale beyond
        // a handful of rows. None of these are uniqueness constraints.
        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.AssigneeId);
        builder.HasIndex(x => x.AuthorId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Priority);
    }
}
