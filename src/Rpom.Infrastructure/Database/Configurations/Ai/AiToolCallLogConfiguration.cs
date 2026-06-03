using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Ai;

namespace Rpom.Infrastructure.Database.Configurations.Ai;

internal sealed class AiToolCallLogConfiguration : IEntityTypeConfiguration<AiToolCallLog>
{
    public void Configure(EntityTypeBuilder<AiToolCallLog> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ToolName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.InputJson).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.OutputJson).HasColumnType("jsonb");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue(AiToolCallStatus.Success);
        builder.Property(x => x.ErrorMessage).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_ai_tool_call_log_status",
            "status IN ('SUCCESS', 'ERROR', 'TIMEOUT', 'REJECTED_PERMISSION')"));

        builder.HasIndex(x => x.MessageId);
        builder.HasIndex(x => new { x.ToolName, x.CreatedAt }).HasDatabaseName("ix_ai_tool_call_log_tool_time");
        builder.HasIndex(x => x.Status).HasDatabaseName("ix_ai_tool_call_log_status");

        builder.HasOne(x => x.Message)
            .WithMany(x => x.ToolCalls)
            .HasForeignKey(x => x.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
