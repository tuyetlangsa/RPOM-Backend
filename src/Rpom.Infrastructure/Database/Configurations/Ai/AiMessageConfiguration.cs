using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Ai;

namespace Rpom.Infrastructure.Database.Configurations.Ai;

internal sealed class AiMessageConfiguration : IEntityTypeConfiguration<AiMessage>
{
    public void Configure(EntityTypeBuilder<AiMessage> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Role).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Content).IsRequired().HasColumnType("text");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_ai_message_role",
            "role IN ('USER', 'ASSISTANT', 'TOOL_CALL', 'SYSTEM')"));

        builder.HasIndex(x => new { x.ConversationId, x.SequenceNumber }).IsUnique()
            .HasDatabaseName("ux_ai_message_conv_seq");
        builder.HasIndex(x => x.ConversationId);

        builder.HasOne(x => x.Conversation)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
