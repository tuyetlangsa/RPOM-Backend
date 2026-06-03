using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Menu;

namespace Rpom.Infrastructure.Database.Configurations.Menu;

internal sealed class PriceVariantAreaConfiguration : IEntityTypeConfiguration<PriceVariantArea>
{
    public void Configure(EntityTypeBuilder<PriceVariantArea> builder)
    {
        builder.HasKey(x => new { x.PriceVariantId, x.AreaId });

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(x => x.AreaId).HasDatabaseName("ix_price_variant_area_area");

        builder.HasOne(x => x.PriceVariant)
            .WithMany(x => x.PriceVariantAreas)
            .HasForeignKey(x => x.PriceVariantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Area)
            .WithMany()
            .HasForeignKey(x => x.AreaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
