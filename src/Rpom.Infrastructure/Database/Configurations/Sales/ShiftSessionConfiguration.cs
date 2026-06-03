using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Sales;

namespace Rpom.Infrastructure.Database.Configurations.Sales;

internal sealed class ShiftSessionConfiguration : IEntityTypeConfiguration<ShiftSession>
{
    public void Configure(EntityTypeBuilder<ShiftSession> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.HasCashTracking).HasDefaultValue(false);
        builder.Property(x => x.OpeningCash).HasPrecision(18, 2);
        builder.Property(x => x.ExpectedClosingCash).HasPrecision(18, 2);
        builder.Property(x => x.ActualClosingCash).HasPrecision(18, 2);
        builder.Property(x => x.Variance).HasPrecision(18, 2);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue(ShiftSessionStatus.Open);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(0);

        // XOR scope: exactly one of (counter_id, kitchen_station_id) must be set
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_shift_session_scope_xor",
            "(counter_id IS NULL) <> (kitchen_station_id IS NULL)"));

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_shift_session_status",
            "status IN ('OPEN', 'CLOSED')"));

        // Cash columns must be set together with HasCashTracking flag (sanity check)
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_shift_session_cash_tracking_counter",
            "has_cash_tracking = false OR counter_id IS NOT NULL"));

        builder.HasIndex(x => x.StaffAccountId);
        builder.HasIndex(x => x.ShiftId);
        builder.HasIndex(x => x.OpenedAt);
        builder.HasIndex(x => x.KitchenStationId).HasDatabaseName("ix_shift_session_kitchen_station");

        // 1 OPEN session per staff at a time
        builder.HasIndex(x => x.StaffAccountId)
            .IsUnique()
            .HasFilter("status = 'OPEN'")
            .HasDatabaseName("ux_shift_session_staff_open");

        // 1 OPEN cashier session per counter (1 cash drawer per counter)
        builder.HasIndex(x => x.CounterId)
            .IsUnique()
            .HasFilter("status = 'OPEN' AND has_cash_tracking = true")
            .HasDatabaseName("ux_shift_session_counter_cashier_open");

        // Non-unique index for quick "find active session at counter" query
        builder.HasIndex(x => new { x.CounterId, x.Status }).HasDatabaseName("ix_shift_session_counter_status");

        builder.HasOne(x => x.Shift)
            .WithMany()
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Staff)
            .WithMany()
            .HasForeignKey(x => x.StaffAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Counter)
            .WithMany()
            .HasForeignKey(x => x.CounterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.KitchenStation)
            .WithMany()
            .HasForeignKey(x => x.KitchenStationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
