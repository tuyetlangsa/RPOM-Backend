using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Sales;

namespace Rpom.Infrastructure.Database.Configurations.Sales;

internal sealed class TicketInvoiceLineConfiguration : IEntityTypeConfiguration<TicketInvoiceLine>
{
    public void Configure(EntityTypeBuilder<TicketInvoiceLine> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ItemCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ItemName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.UomCode).IsRequired().HasMaxLength(20);
        builder.Property(x => x.UomName).IsRequired().HasMaxLength(50);

        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.ChoicePricePerUnit).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.VatPercent).HasPrecision(5, 2).HasDefaultValue(0m);
        builder.Property(x => x.ServiceChargePercent).HasPrecision(5, 2).HasDefaultValue(0m);
        builder.Property(x => x.ServiceChargeVatPercent).HasPrecision(5, 2).HasDefaultValue(0m);
        builder.Property(x => x.LineDiscountPercent).HasPrecision(9, 6).HasDefaultValue(0m);
        builder.Property(x => x.TicketDiscountPercent).HasPrecision(9, 6).HasDefaultValue(0m);
        builder.Property(x => x.LineSubtotal).HasPrecision(18, 2);
        builder.Property(x => x.TotalDiscount).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.ServiceChargeAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.VatAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.DisplayOrder).HasDefaultValue(0);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(x => x.TicketInvoiceId);

        builder.HasOne(x => x.TicketInvoice)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.TicketInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
