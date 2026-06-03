using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Sales;

namespace Rpom.Infrastructure.Database.Configurations.Sales;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        // "Order" is a SQL reserved keyword; force plural lowercase
        builder.ToTable("orders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue(OrderStatus.Draft);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(0);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_order_status",
            "status IN ('DRAFT', 'SENT', 'PROCESSING', 'DONE', 'DELETED')"));

        builder.HasIndex(x => x.TicketId);
        builder.HasIndex(x => new { x.TicketId, x.OrderNumber }).IsUnique().HasDatabaseName("ux_order_ticket_sequence");
        builder.HasIndex(x => new { x.Status, x.UpdatedAt }).HasDatabaseName("ix_order_status_updated");

        builder.HasOne(x => x.Ticket)
            .WithMany(x => x.Orders)
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CreatedByStaff)
            .WithMany()
            .HasForeignKey(x => x.CreatedByStaffId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
