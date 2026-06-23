using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Sales;

namespace Rpom.Infrastructure.Database.Configurations.Sales;

internal sealed class TicketPaymentDetailConfiguration : IEntityTypeConfiguration<TicketPaymentDetail>
{
    public void Configure(EntityTypeBuilder<TicketPaymentDetail> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue(TicketPaymentStatus.Pending);
        builder.Property(x => x.TransactionRef).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_ticket_payment_detail_status",
            "status IN ('PENDING', 'SUCCESS', 'CANCELLED', 'DELETED')"));

        builder.HasIndex(x => x.TicketId);
        builder.HasIndex(x => new { x.TicketId, x.Status }).HasDatabaseName("ix_ticket_payment_detail_ticket_status");
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ParentPaymentDetailId);

        builder.HasOne(x => x.ParentPaymentDetail)
            .WithMany(x => x.ChildPaymentDetails)
            .HasForeignKey(x => x.ParentPaymentDetailId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered unique: dedup vendor callback retries.
        // Cash payments (TransactionRef=NULL) are exempt — many cash rows per ticket allowed.
        builder.HasIndex(x => new { x.TicketId, x.TransactionRef })
            .IsUnique()
            .HasFilter("transaction_ref IS NOT NULL")
            .HasDatabaseName("ux_ticket_payment_detail_tx_ref");

        builder.HasOne(x => x.Ticket)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.PaymentMethod)
            .WithMany()
            .HasForeignKey(x => x.PaymentMethodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProcessedByStaff)
            .WithMany()
            .HasForeignKey(x => x.ProcessedByStaffId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Rpom.Domain.Operations.PosTerminal>()
            .WithMany()
            .HasForeignKey(x => x.PosTerminalId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
