using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Reservation;
using ReservationEntity = Rpom.Domain.Reservation.Reservation;

namespace Rpom.Infrastructure.Database.Configurations.Reservation;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<ReservationEntity>
{
    public void Configure(EntityTypeBuilder<ReservationEntity> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(30);
        builder.Property(x => x.CustomerName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.CustomerPhone).IsRequired().HasMaxLength(20);
        builder.Property(x => x.GuestCount).HasDefaultValue((short)1);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20)
            .HasDefaultValue(ReservationStatus.Booked);
        builder.Property(x => x.CancellationNote).HasMaxLength(500);
        builder.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(0);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_reservation_status",
            "status IN ('BOOKED', 'ARRIVED', 'CANCELLED', 'NOT_ARRIVED')"));

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.CounterId, x.Status, x.TargetTime })
            .HasDatabaseName("ix_reservation_counter_status_target_time");
        builder.HasIndex(x => x.UpdatedAt).HasDatabaseName("ix_reservation_updated_at");
        builder.HasIndex(x => x.CreatedByStaffId);
        builder.HasIndex(x => x.CustomerPhone).HasDatabaseName("ix_reservation_phone");

        builder.HasOne(x => x.Counter)
            .WithMany()
            .HasForeignKey(x => x.CounterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CancellationReason)
            .WithMany()
            .HasForeignKey(x => x.CancellationReasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByStaff)
            .WithMany()
            .HasForeignKey(x => x.CreatedByStaffId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.ReservationTables)
            .WithOne(rt => rt.Reservation)
            .HasForeignKey(rt => rt.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
