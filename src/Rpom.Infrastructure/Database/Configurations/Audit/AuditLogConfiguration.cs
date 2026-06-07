using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Audit;

namespace Rpom.Infrastructure.Database.Configurations.Audit;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EntityType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(30);
        builder.Property(x => x.ActorFullName).HasMaxLength(200);
        builder.Property(x => x.Timestamp).HasDefaultValueSql("now()");
        builder.Property(x => x.Summary).HasMaxLength(500);

        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.Timestamp })
            .HasDatabaseName("ix_audit_log_entity_time");
        builder.HasIndex(x => new { x.ActorStaffAccountId, x.Timestamp }).HasDatabaseName("ix_audit_log_actor_time");
        builder.HasIndex(x => x.Timestamp).HasDatabaseName("ix_audit_log_time");

        // No FK on ActorStaffAccountId — intentional soft ref (Audit centralization rule).
        // No FK on EntityId — polymorphic.
    }
}
