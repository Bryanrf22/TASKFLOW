using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Core.Domain.Entities;
using TaskFlow.Core.Domain.Enums;

namespace TaskFlow.Data.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.UserId).IsRequired().HasMaxLength(450);

        builder.Property(n => n.Type)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(n => n.Message).IsRequired().HasMaxLength(2000);

        builder.HasIndex(n => new { n.UserId, n.IsRead });

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(n => n.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(n => n.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}