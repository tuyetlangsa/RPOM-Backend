using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Menu;

namespace Rpom.Infrastructure.Database.Configurations.Menu;

internal sealed class PriceEntryConfiguration : IEntityTypeConfiguration<PriceEntry>
{
    public void Configure(EntityTypeBuilder<PriceEntry> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.IsVatIncluded).HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(x => x.PriceVariantId);
        builder.HasIndex(x => x.ItemId);
        builder.HasIndex(x => new { x.PriceVariantId, x.ItemId }).IsUnique()
            .HasDatabaseName("ux_price_entry_variant_item");

        builder.HasOne(x => x.PriceVariant)
            .WithMany(x => x.PriceEntries)
            .HasForeignKey(x => x.PriceVariantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Item)
            .WithMany(x => x.PriceEntries)
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
