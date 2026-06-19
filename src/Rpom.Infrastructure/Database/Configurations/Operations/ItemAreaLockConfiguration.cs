using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Operations;

namespace Rpom.Infrastructure.Database.Configurations.Operations;

internal sealed class ItemAreaLockConfiguration : IEntityTypeConfiguration<ItemAreaLock>
{
    public void Configure(EntityTypeBuilder<ItemAreaLock> builder)
    {
        // Presence = locked. Composite PK guarantees one row per (Item, Area).
        builder.HasKey(x => new { x.ItemId, x.AreaId });

        builder.Property(x => x.Note).HasMaxLength(200);
        builder.Property(x => x.LockedAt).HasDefaultValueSql("now()");

        // GetMenu / kitchen view query by area: WHERE AreaId = X.
        builder.HasIndex(x => x.AreaId).HasDatabaseName("ix_item_area_lock_area");

        builder.HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Area)
            .WithMany()
            .HasForeignKey(x => x.AreaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
