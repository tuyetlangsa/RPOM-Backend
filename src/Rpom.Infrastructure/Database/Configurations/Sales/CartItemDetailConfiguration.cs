using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Sales;

namespace Rpom.Infrastructure.Database.Configurations.Sales;

internal sealed class CartItemDetailConfiguration : IEntityTypeConfiguration<CartItemDetail>
{
    public void Configure(EntityTypeBuilder<CartItemDetail> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ItemName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ComponentType).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Quantity).HasPrecision(18, 3).HasDefaultValue(1m);
        builder.Property(x => x.ExtraPrice).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.Notes).HasMaxLength(300);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_cart_item_detail_component_type",
            "component_type IN ('MAIN_COMPONENT', 'MODIFIER')"));

        builder.HasIndex(x => x.CartItemId);
        builder.HasIndex(x => new { x.CartItemId, x.ComponentType }).HasDatabaseName("ix_cart_item_detail_item_type");

        builder.HasOne(x => x.CartItem)
            .WithMany(x => x.Details)
            .HasForeignKey(x => x.CartItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ChoiceCategory)
            .WithMany()
            .HasForeignKey(x => x.ChoiceCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
