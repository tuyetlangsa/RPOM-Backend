using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Sales;

namespace Rpom.Infrastructure.Database.Configurations.Sales;

internal sealed class OrderItemDetailConfiguration : IEntityTypeConfiguration<OrderItemDetail>
{
    public void Configure(EntityTypeBuilder<OrderItemDetail> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ItemName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ComponentType).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Quantity).HasPrecision(18, 3).HasDefaultValue(1m);
        builder.Property(x => x.ExtraPrice).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.Notes).HasMaxLength(300);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_order_item_detail_component_type",
            "component_type IN ('MAIN_COMPONENT', 'MODIFIER')"));

        builder.HasIndex(x => x.OrderItemId);

        builder.HasOne(x => x.OrderItem)
            .WithMany(x => x.Details)
            .HasForeignKey(x => x.OrderItemId)
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
