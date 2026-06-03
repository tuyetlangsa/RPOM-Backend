using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Menu;

namespace Rpom.Infrastructure.Database.Configurations.Menu;

internal sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
        builder.Property(x => x.VatPercent).HasPrecision(5, 2).HasDefaultValue(0m);
        builder.Property(x => x.IsStockable).HasDefaultValue(false);
        builder.Property(x => x.HasRecipe).HasDefaultValue(false);
        builder.Property(x => x.LowStockThreshold).HasPrecision(18, 3);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.IsStockable).HasDatabaseName("ix_item_stockable");
        builder.HasIndex(x => x.KitchenStationId).HasDatabaseName("ix_item_kitchen_station");

        builder.HasOne(x => x.BaseUom)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.BaseUomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.KitchenStation)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.KitchenStationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
