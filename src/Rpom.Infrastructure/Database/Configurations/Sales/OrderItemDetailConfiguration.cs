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
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue(OrderItemStatus.Pending);
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_order_item_detail_component_type",
            "component_type IN ('MAIN_COMPONENT', 'MODIFIER')"));
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_order_item_detail_status",
            "status IN ('PENDING', 'PROCESSING', 'READY', 'DONE', 'CANCELLED')"));

        builder.HasIndex(x => x.OrderItemId);

        builder.HasIndex(x => new { x.KitchenStationId, x.Status })
            .HasDatabaseName("ix_order_item_detail_station_status");

        builder.HasOne(x => x.OrderItem)
            .WithMany(x => x.Details)
            .HasForeignKey(x => x.OrderItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.KitchenStation)
            .WithMany()
            .HasForeignKey(x => x.KitchenStationId)
            .OnDelete(DeleteBehavior.SetNull);

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
