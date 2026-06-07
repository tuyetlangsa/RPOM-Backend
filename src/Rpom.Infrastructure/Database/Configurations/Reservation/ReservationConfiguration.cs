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
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue(ReservationStatus.Booked);
        builder.Property(x => x.CancellationNote).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_reservation_status",
            "status IN ('BOOKED', 'ARRIVED', 'CANCELLED')"));

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.TableId);
        builder.HasIndex(x => new { x.TableId, x.Status, x.TargetTime }).HasDatabaseName("ix_reservation_table_active");
        builder.HasIndex(x => new { x.Status, x.TargetTime }).HasDatabaseName("ix_reservation_status_target_time");
        builder.HasIndex(x => x.LinkedTicketId);
        builder.HasIndex(x => x.CreatedByStaffId);
        builder.HasIndex(x => x.CustomerPhone).HasDatabaseName("ix_reservation_phone");

        builder.HasOne(x => x.Table)
            .WithMany()
            .HasForeignKey(x => x.TableId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CancellationReason)
            .WithMany()
            .HasForeignKey(x => x.CancellationReasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.LinkedTicket)
            .WithMany()
            .HasForeignKey(x => x.LinkedTicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByStaff)
            .WithMany()
            .HasForeignKey(x => x.CreatedByStaffId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
