using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Operations;

namespace Rpom.Infrastructure.Database.Configurations.Operations;

internal sealed class KitchenOrderPinConfiguration : IEntityTypeConfiguration<KitchenOrderPin>
{
    public void Configure(EntityTypeBuilder<KitchenOrderPin> builder)
    {
        builder.HasKey(x => new { x.KitchenStationId, x.OrderId });

        builder.Property(x => x.PinnedAt).HasDefaultValueSql("now()");

        // KDS query: pins of a station.
        builder.HasIndex(x => x.KitchenStationId).HasDatabaseName("ix_kitchen_order_pin_station");

        builder.HasOne(x => x.KitchenStation)
            .WithMany()
            .HasForeignKey(x => x.KitchenStationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
