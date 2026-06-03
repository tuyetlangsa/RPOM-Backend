using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Inventory;

namespace Rpom.Infrastructure.Database.Configurations.Inventory;

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MovementType).IsRequired().HasMaxLength(20);
        builder.Property(x => x.QtyInBase).HasPrecision(22, 5);
        builder.Property(x => x.BalanceAfter).HasPrecision(22, 5);
        builder.Property(x => x.ReferenceType).HasMaxLength(20);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_stock_movement_type",
            "movement_type IN ('STOCK_IN', 'ADJUST_IN', 'ADJUST_OUT', 'DEDUCT')"));

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_stock_movement_reference_type",
            "reference_type IS NULL OR reference_type IN ('ORDER_DISH', 'MANUAL')"));

        builder.HasIndex(x => new { x.ItemId, x.CreatedAt }).HasDatabaseName("ix_stock_movement_item_time");
        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId }).HasDatabaseName("ix_stock_movement_ref");
        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_stock_movement_time");
        builder.HasIndex(x => x.MovementType);

        builder.HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByStaff)
            .WithMany()
            .HasForeignKey(x => x.CreatedByStaffId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
