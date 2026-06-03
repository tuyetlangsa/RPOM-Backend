using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Sales;

namespace Rpom.Infrastructure.Database.Configurations.Sales;

internal sealed class EInvoiceConfiguration : IEntityTypeConfiguration<EInvoice>
{
    public void Configure(EntityTypeBuilder<EInvoice> builder)
    {
        // 1:1 specialization: PK = FK = TicketId
        builder.HasKey(x => x.TicketId);

        builder.Property(x => x.TicketId).ValueGeneratedNever();
        builder.Property(x => x.CustomerName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.TaxCode).HasMaxLength(20);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.ExternalInvoiceNumber).HasMaxLength(50);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasOne(x => x.Ticket)
            .WithOne(x => x.EInvoice)
            .HasForeignKey<EInvoice>(x => x.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
