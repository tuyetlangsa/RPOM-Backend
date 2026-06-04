using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Menu;

namespace Rpom.Infrastructure.Database.Configurations.Menu;

internal sealed class PriceVariantConfiguration : IEntityTypeConfiguration<PriceVariant>
{
    public void Configure(EntityTypeBuilder<PriceVariant> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.AppliesToAllAreas).HasDefaultValue(true);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.Ignore(x => x.Specificity);

        builder.HasIndex(x => x.PriceTableId);
        builder.HasIndex(x => new { x.PriceTableId, x.Code }).IsUnique().HasDatabaseName("ux_price_variant_table_code");
        builder.HasIndex(x => new { x.PriceTableId, x.IsActive }).HasDatabaseName("ix_price_variant_active");

        builder.HasOne(x => x.PriceTable)
            .WithMany(x => x.PriceVariants)
            .HasForeignKey(x => x.PriceTableId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
