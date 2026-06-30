using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Sales;

namespace Rpom.Infrastructure.Database.Configurations.Sales;

internal sealed class TicketInvoiceConfiguration : IEntityTypeConfiguration<TicketInvoice>
{
    public void Configure(EntityTypeBuilder<TicketInvoice> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TicketCode).IsRequired().HasMaxLength(30);
        builder.Property(x => x.TableCode).IsRequired().HasMaxLength(20);
        builder.Property(x => x.WaiterName).HasMaxLength(100);
        builder.Property(x => x.ClosedByName).HasMaxLength(100);

        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.DiscountPercent).HasPrecision(9, 6).HasDefaultValue(0m);
        builder.Property(x => x.ServiceChargeAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.ServiceChargePercent).HasPrecision(5, 2).HasDefaultValue(0m);
        builder.Property(x => x.VatAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.RoundingAdjustment).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.PaidAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.RefundAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.ChangeAmount).HasPrecision(18, 2).HasDefaultValue(0m);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        // 1:1 with Ticket
        builder.HasIndex(x => x.TicketId).IsUnique();

        // Report query indexes
        builder.HasIndex(x => x.ClosedAt);
        builder.HasIndex(x => new { x.CounterId, x.ClosedAt });
        builder.HasIndex(x => new { x.ShiftId, x.ClosedAt });

        builder.HasOne(x => x.Ticket)
            .WithOne()
            .HasForeignKey<TicketInvoice>(x => x.TicketId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
