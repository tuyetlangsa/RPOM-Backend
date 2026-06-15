using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Sales;

namespace Rpom.Infrastructure.Database.Configurations.Sales;

internal sealed class TicketItemSumConfiguration : IEntityTypeConfiguration<TicketItemSum>
{
    public void Configure(EntityTypeBuilder<TicketItemSum> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ItemCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ItemName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.UomCode).IsRequired().HasMaxLength(20);
        builder.Property(x => x.UomName).IsRequired().HasMaxLength(50);

        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.LineDiscountPercent).HasPrecision(9, 6).HasDefaultValue(0m);
        builder.Property(x => x.TicketDiscountPercent).HasPrecision(9, 6).HasDefaultValue(0m);
        builder.Property(x => x.ServiceChargeVatPercent).HasPrecision(5, 2).HasDefaultValue(0m);
        builder.Property(x => x.ChoicePricePerUnit).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.VatPercent).HasPrecision(5, 2).HasDefaultValue(0m);
        builder.Property(x => x.ServiceChargePercent).HasPrecision(5, 2).HasDefaultValue(0m);

        builder.Property(x => x.TotalQuantity).HasPrecision(22, 3).HasDefaultValue(0m);
        builder.Property(x => x.TotalLineSubtotal).HasPrecision(22, 2).HasDefaultValue(0m);
        builder.Property(x => x.TotalServiceCharge).HasPrecision(22, 2).HasDefaultValue(0m);
        builder.Property(x => x.TotalDiscount).HasPrecision(22, 2).HasDefaultValue(0m);
        builder.Property(x => x.TotalVat).HasPrecision(22, 2).HasDefaultValue(0m);
        builder.Property(x => x.TotalAmount).HasPrecision(22, 2).HasDefaultValue(0m);

        builder.Property(x => x.DisplayOrder).HasDefaultValue(0);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(x => x.TicketId);
        builder.HasIndex(x => new
        {
            x.TicketId,
            x.ItemId,
            x.UomId,
            x.UnitPrice,
            x.ChoicePricePerUnit,
            x.LineDiscountPercent,
            x.TicketDiscountPercent,
            x.VatPercent,
            x.ServiceChargePercent,
            x.ServiceChargeVatPercent
        })
            .IsUnique().HasDatabaseName("ux_ticket_item_sum_bucket");
        builder.HasIndex(x => new { x.TicketId, x.DisplayOrder }).HasDatabaseName("ix_ticket_item_sum_render");

        builder.HasOne(x => x.Ticket)
            .WithMany(x => x.ItemSums)
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Uom)
            .WithMany()
            .HasForeignKey(x => x.UomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
