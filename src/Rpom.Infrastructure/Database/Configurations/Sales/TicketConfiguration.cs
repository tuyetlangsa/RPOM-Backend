using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Sales;

namespace Rpom.Infrastructure.Database.Configurations.Sales;

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(30);
        builder.Property(x => x.GuestCount).HasDefaultValue((short)1);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue(TicketStatus.Open);
        builder.Property(x => x.OpenedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.CancellationNote).HasMaxLength(500);

        builder.Property(x => x.Subtotal).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.ServiceChargePercent).HasPrecision(5, 2).HasDefaultValue(0m);
        builder.Property(x => x.ServiceChargeAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.VatPercent).HasPrecision(5, 2).HasDefaultValue(0m);
        builder.Property(x => x.VatAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.PaidAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.ChangeAmount).HasPrecision(18, 2).HasDefaultValue(0m);

        builder.Property(x => x.GuestQrToken).HasMaxLength(20);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(0);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_ticket_status",
            "status IN ('OPEN', 'CLOSED', 'CANCELLED')"));

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.CounterId, x.Status, x.UpdatedAt }).HasDatabaseName("ix_ticket_counter_active");
        builder.HasIndex(x => new { x.AreaId, x.Status }).HasDatabaseName("ix_ticket_area_status");
        builder.HasIndex(x => x.TableId);
        builder.HasIndex(x => x.ShiftSessionId);
        builder.HasIndex(x => x.Status);

        // Filtered unique index — Postgres native syntax via HasFilter
        builder.HasIndex(x => x.GuestQrToken)
            .IsUnique()
            .HasFilter("guest_qr_token IS NOT NULL")
            .HasDatabaseName("ux_ticket_guest_qr_token");

        builder.HasOne(x => x.Table)
            .WithMany()
            .HasForeignKey(x => x.TableId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Area)
            .WithMany()
            .HasForeignKey(x => x.AreaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Counter)
            .WithMany()
            .HasForeignKey(x => x.CounterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ShiftSession)
            .WithMany(x => x.Tickets)
            .HasForeignKey(x => x.ShiftSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Shift)
            .WithMany()
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.WaiterStaff)
            .WithMany()
            .HasForeignKey(x => x.WaiterStaffId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ManagerStaff)
            .WithMany()
            .HasForeignKey(x => x.ManagerStaffId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CancellationReason)
            .WithMany()
            .HasForeignKey(x => x.CancellationReasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DiscountPolicy)
            .WithMany()
            .HasForeignKey(x => x.DiscountPolicyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
