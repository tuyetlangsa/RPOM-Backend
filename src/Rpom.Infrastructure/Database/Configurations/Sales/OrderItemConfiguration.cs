using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Sales;

namespace Rpom.Infrastructure.Database.Configurations.Sales;

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ItemCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ItemName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Quantity).HasPrecision(18, 3).HasDefaultValue(1m);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue(OrderItemStatus.Pending);
        builder.Property(x => x.SentAt).HasDefaultValueSql("now()");
        builder.Property(x => x.CancellationNote).HasMaxLength(500);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(0);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_order_item_status",
            "status IN ('PENDING', 'PROCESSING', 'READY', 'DONE', 'CANCELLED')"));

        builder.HasIndex(x => x.OrderId);
        builder.HasIndex(x => x.TicketId);
        builder.HasIndex(x => new { x.KitchenStationId, x.Status, x.UpdatedAt }).HasDatabaseName("ix_order_item_kds");
        builder.HasIndex(x => new { x.TicketId, x.Status }).HasDatabaseName("ix_order_item_ticket_status");
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.OriginalOrderItemId).HasDatabaseName("ix_order_item_original");

        builder.HasOne(x => x.Order)
            .WithMany(x => x.OrderItems)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Ticket)
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Uom)
            .WithMany()
            .HasForeignKey(x => x.UomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.KitchenStation)
            .WithMany()
            .HasForeignKey(x => x.KitchenStationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.CancellationReason)
            .WithMany()
            .HasForeignKey(x => x.CancellationReasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CancelledByStaff)
            .WithMany()
            .HasForeignKey(x => x.CancelledByStaffId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.OriginalOrderItem)
            .WithMany(x => x.RefundLines)
            .HasForeignKey(x => x.OriginalOrderItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
