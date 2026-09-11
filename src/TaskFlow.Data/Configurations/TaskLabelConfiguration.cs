using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Core.Domain.Entities;

namespace TaskFlow.Data.Configurations;

public sealed class TaskLabelConfiguration : IEntityTypeConfiguration<TaskLabel>
{
    public void Configure(EntityTypeBuilder<TaskLabel> builder)
    {
        builder.HasKey(tl => new { tl.TaskId, tl.LabelId });

        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(tl => tl.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Label>()
            .WithMany()
            .HasForeignKey(tl => tl.LabelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}