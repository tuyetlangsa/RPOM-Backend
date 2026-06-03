using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Ai;

namespace Rpom.Infrastructure.Database.Configurations.Ai;

internal sealed class AiNotificationConfiguration : IEntityTypeConfiguration<AiNotification>
{
    public void Configure(EntityTypeBuilder<AiNotification> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Body).IsRequired().HasColumnType("text");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue(AiNotificationStatus.Unread);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_ai_notification_type",
            "type IN ('LOW_STOCK', 'EOD_SUMMARY')"));

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_ai_notification_status",
            "status IN ('UNREAD', 'READ', 'DISMISSED', 'ARCHIVED')"));

        builder.HasIndex(x => new { x.RecipientStaffId, x.Status, x.UpdatedAt }).HasDatabaseName("ix_ai_notification_recipient_status");
        builder.HasIndex(x => new { x.Type, x.CreatedAt }).HasDatabaseName("ix_ai_notification_type_time");
        builder.HasIndex(x => x.RefItemId).HasDatabaseName("ix_ai_notification_ref_item");

        builder.HasOne(x => x.RecipientStaff)
            .WithMany()
            .HasForeignKey(x => x.RecipientStaffId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
