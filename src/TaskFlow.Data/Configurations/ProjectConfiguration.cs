using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Core.Domain.Entities;

namespace TaskFlow.Data.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Key)
            .IsRequired()
            .HasMaxLength(10);

        builder.HasIndex(p => p.Key).IsUnique();

        builder.Property(p => p.Description).HasMaxLength(2000);

        builder.Property(p => p.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(450);
    }
}