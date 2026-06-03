using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Ai;

namespace Rpom.Infrastructure.Database.Configurations.Ai;

internal sealed class AiConversationConfiguration : IEntityTypeConfiguration<AiConversation>
{
    public void Configure(EntityTypeBuilder<AiConversation> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue(AiConversationStatus.Active);
        builder.Property(x => x.StartedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.MessageCount).HasDefaultValue(0);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_ai_conversation_status",
            "status IN ('ACTIVE', 'ENDED')"));

        builder.HasIndex(x => x.OwnerStaffId);
        builder.HasIndex(x => new { x.OwnerStaffId, x.UpdatedAt }).HasDatabaseName("ix_ai_conversation_owner_updated");
        builder.HasIndex(x => x.Status);

        builder.HasOne(x => x.OwnerStaff)
            .WithMany()
            .HasForeignKey(x => x.OwnerStaffId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
