using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Sales;

namespace Rpom.Infrastructure.Database.Configurations.Sales;

internal sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Gateway).IsRequired().HasMaxLength(50).HasDefaultValue("SEPAY");
        builder.Property(x => x.BankBrand).HasMaxLength(50);
        builder.Property(x => x.AccountNumber).HasMaxLength(50);
        builder.Property(x => x.SubAccount).HasMaxLength(50);
        builder.Property(x => x.TransferType).IsRequired().HasMaxLength(10);
        builder.Property(x => x.TransferAmount).HasPrecision(18, 2);
        builder.Property(x => x.Accumulated).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.Code).HasMaxLength(100);
        builder.Property(x => x.Content).HasMaxLength(1000);
        builder.Property(x => x.ReferenceCode).HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.MatchedReferenceCode).HasMaxLength(100);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20)
            .HasDefaultValue(PaymentTransactionStatus.Unmatched);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_payment_transaction_status",
            "status IN ('MATCHED', 'UNMATCHED', 'MISMATCH', 'DUPLICATE', 'IGNORED')"));

        // Idempotency: one row per gateway transaction id — dedup webhook retries.
        builder.HasIndex(x => x.GatewayTransactionId)
            .IsUnique()
            .HasDatabaseName("ux_payment_transaction_gateway_tx_id");

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.MatchedPaymentDetailId);

        builder.HasOne(x => x.MatchedPaymentDetail)
            .WithMany()
            .HasForeignKey(x => x.MatchedPaymentDetailId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
