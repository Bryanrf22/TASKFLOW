using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Core.Domain.Entities;
using TaskFlow.Core.Domain.Enums;

namespace TaskFlow.Data.Configurations;

public sealed class TaskHistoryConfiguration : IEntityTypeConfiguration<TaskHistoryEntry>
{
    public void Configure(EntityTypeBuilder<TaskHistoryEntry> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.ChangeType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(h => h.ActorUserId).IsRequired().HasMaxLength(450);
        builder.Property(h => h.Field).HasMaxLength(50);
        builder.Property(h => h.FromValue).HasMaxLength(255);
        builder.Property(h => h.ToValue).HasMaxLength(255);

        builder.HasIndex(h => h.TaskId);

        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(h => h.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}