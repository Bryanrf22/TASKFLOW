using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Core.Domain.Entities;

namespace TaskFlow.Data.Configurations;

public sealed class TaskCommentConfiguration : IEntityTypeConfiguration<TaskComment>
{
    public void Configure(EntityTypeBuilder<TaskComment> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.AuthorUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(c => c.Content)
            .IsRequired()
            .HasMaxLength(4000);

        builder.HasIndex(c => c.TaskId);
        builder.HasIndex(c => c.AuthorUserId);

        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(c => c.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(c => c.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}