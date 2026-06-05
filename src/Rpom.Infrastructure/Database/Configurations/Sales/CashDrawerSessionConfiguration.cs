using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Sales.CashDrawer;

namespace Rpom.Infrastructure.Database.Configurations.Sales;

internal sealed class CashDrawerSessionConfiguration : IEntityTypeConfiguration<CashDrawerSession>
{
    public void Configure(EntityTypeBuilder<CashDrawerSession> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OpeningCash).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.ExpectedClosingCash).HasPrecision(18, 2);
        builder.Property(x => x.ActualClosingCash).HasPrecision(18, 2);
        builder.Property(x => x.Variance).HasPrecision(18, 2);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue(CashDrawerStatus.Open);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(0);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_cash_drawer_session_status",
            "status IN ('OPEN', 'CLOSED')"));

        builder.HasIndex(x => x.OpenedByStaffAccountId);
        builder.HasIndex(x => x.ClosedByStaffAccountId);
        builder.HasIndex(x => x.OpenedAt);

        // 1 OPEN cash drawer per counter at a time
        builder.HasIndex(x => x.CounterId)
            .IsUnique()
            .HasFilter("status = 'OPEN'")
            .HasDatabaseName("ux_cash_drawer_session_counter_open");

        // Non-unique index for quick "find active drawer at counter" query
        builder.HasIndex(x => new { x.CounterId, x.Status })
            .HasDatabaseName("ix_cash_drawer_session_counter_status");

        builder.HasOne(x => x.Counter)
            .WithMany()
            .HasForeignKey(x => x.CounterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.OpenedByStaff)
            .WithMany()
            .HasForeignKey(x => x.OpenedByStaffAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ClosedByStaff)
            .WithMany()
            .HasForeignKey(x => x.ClosedByStaffAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
