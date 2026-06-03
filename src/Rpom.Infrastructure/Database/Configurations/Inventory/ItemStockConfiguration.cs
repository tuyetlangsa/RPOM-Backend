using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Inventory;

namespace Rpom.Infrastructure.Database.Configurations.Inventory;

internal sealed class ItemStockConfiguration : IEntityTypeConfiguration<ItemStock>
{
    public void Configure(EntityTypeBuilder<ItemStock> builder)
    {
        // 1:0..1 with Item — PK = FK = ItemId
        builder.HasKey(x => x.ItemId);

        builder.Property(x => x.ItemId).ValueGeneratedNever();
        builder.Property(x => x.CurrentQty).HasPrecision(22, 5).HasDefaultValue(0m);
        builder.Property(x => x.ReservedQty).HasPrecision(22, 5).HasDefaultValue(0m);
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(x => x.UpdatedAt).HasDatabaseName("ix_item_stock_updated");
        builder.HasIndex(x => x.CurrentQty).HasDatabaseName("ix_item_stock_qty");

        builder.HasOne(x => x.Item)
            .WithOne()
            .HasForeignKey<ItemStock>(x => x.ItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
