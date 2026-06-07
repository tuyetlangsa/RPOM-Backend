using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Restaurant;

namespace Rpom.Infrastructure.Database.Configurations.Restaurant;

internal sealed class TableLockConfiguration : IEntityTypeConfiguration<TableLock>
{
    public void Configure(EntityTypeBuilder<TableLock> builder)
    {
        builder.HasKey(x => x.TableId);

        // TableId is also the FK — no surrogate key, one lock per table.
        builder.Property(x => x.TableId).ValueGeneratedNever();
        builder.Property(x => x.StaffName).IsRequired().HasMaxLength(150);

        builder.HasIndex(x => x.StaffAccountId).HasDatabaseName("ix_table_lock_staff");

        builder.HasOne(x => x.Table)
            .WithMany()
            .HasForeignKey(x => x.TableId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
