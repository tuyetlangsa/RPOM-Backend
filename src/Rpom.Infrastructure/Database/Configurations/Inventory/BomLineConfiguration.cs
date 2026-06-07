using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Inventory;

namespace Rpom.Infrastructure.Database.Configurations.Inventory;

internal sealed class BomLineConfiguration : IEntityTypeConfiguration<BomLine>
{
    public void Configure(EntityTypeBuilder<BomLine> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity).HasPrecision(18, 5);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(x => x.SellableItemId);
        builder.HasIndex(x => x.MaterialItemId);
        builder.HasIndex(x => new { x.SellableItemId, x.MaterialItemId }).IsUnique()
            .HasDatabaseName("ux_bom_line_recipe");

        builder.HasOne(x => x.SellableItem)
            .WithMany()
            .HasForeignKey(x => x.SellableItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MaterialItem)
            .WithMany()
            .HasForeignKey(x => x.MaterialItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Uom)
            .WithMany()
            .HasForeignKey(x => x.UomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
