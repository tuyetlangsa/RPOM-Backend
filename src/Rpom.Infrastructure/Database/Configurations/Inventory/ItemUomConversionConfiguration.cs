using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Inventory;

namespace Rpom.Infrastructure.Database.Configurations.Inventory;

internal sealed class ItemUomConversionConfiguration : IEntityTypeConfiguration<ItemUomConversion>
{
    public void Configure(EntityTypeBuilder<ItemUomConversion> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FactorToBase).HasPrecision(18, 8);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(x => new { x.ItemId, x.UomId }).IsUnique().HasDatabaseName("ux_item_uom_conversion");
        builder.HasIndex(x => x.ItemId);

        builder.HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Uom)
            .WithMany()
            .HasForeignKey(x => x.UomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
