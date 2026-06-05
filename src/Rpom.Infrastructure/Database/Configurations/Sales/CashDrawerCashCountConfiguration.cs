using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Sales.CashDrawer;

namespace Rpom.Infrastructure.Database.Configurations.Sales;

internal sealed class CashDrawerCashCountConfiguration : IEntityTypeConfiguration<CashDrawerCashCount>
{
    public void Configure(EntityTypeBuilder<CashDrawerCashCount> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Phase).IsRequired().HasMaxLength(10);
        builder.Property(x => x.Quantity).HasDefaultValue(0);
        builder.Property(x => x.Subtotal).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_cash_drawer_cash_count_phase",
            "phase IN ('OPENING', 'CLOSING')"));

        builder.HasIndex(x => new { x.CashDrawerSessionId, x.Phase, x.DenominationId })
            .IsUnique().HasDatabaseName("ux_cash_drawer_cash_count");
        builder.HasIndex(x => new { x.CashDrawerSessionId, x.Phase })
            .HasDatabaseName("ix_cash_drawer_cash_count_session_phase");

        builder.HasOne(x => x.CashDrawerSession)
            .WithMany(x => x.CashCounts)
            .HasForeignKey(x => x.CashDrawerSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Denomination)
            .WithMany()
            .HasForeignKey(x => x.DenominationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
