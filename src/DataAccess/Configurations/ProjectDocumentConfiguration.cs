using BusinessLogic.Documents;
using BusinessLogic.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Configurations;

public class ProjectDocumentConfiguration : IEntityTypeConfiguration<ProjectDocument>
{
    public void Configure(EntityTypeBuilder<ProjectDocument> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.FileName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.StoredName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.ContentType)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.SizeBytes).IsRequired();

        builder.HasIndex(d => d.ProjectId);

        // Bind both sides explicitly: Project.Documents is a read-only
        // collection backed by a private field, and EF needs both endpoints
        // configured here to avoid inferring a second shadow relationship.
        // Cascade: deleting a project removes its document records.
        builder.HasOne(d => d.Project)
            .WithMany(p => p.Documents)
            .HasForeignKey(d => d.ProjectId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
