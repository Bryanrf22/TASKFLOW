using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Core.Domain.Entities;

namespace TaskFlow.Data.Configurations;

public sealed class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(l => l.ColorHex)
            .IsRequired()
            .HasMaxLength(7);

        builder.HasIndex(l => new { l.ProjectId, l.Name }).IsUnique();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(l => l.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}