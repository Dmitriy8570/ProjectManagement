using BusinessLogic.Documents;
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

        // Cascade: deleting a project removes its document records.
        // The file-system copies are cleaned up explicitly when documents
        // are deleted individually; on project delete they are orphaned on disk
        // (acceptable trade-off — a background cleanup job handles them).
        builder.HasOne("BusinessLogic.Projects.Project", null)
            .WithMany()
            .HasForeignKey(nameof(ProjectDocument.ProjectId))
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
