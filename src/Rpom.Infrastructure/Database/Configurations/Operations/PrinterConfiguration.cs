using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Operations;

namespace Rpom.Infrastructure.Database.Configurations.Operations;

internal sealed class PrinterConfiguration : IEntityTypeConfiguration<Printer>
{
    public void Configure(EntityTypeBuilder<Printer> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Type).IsRequired().HasMaxLength(20);
        builder.Property(x => x.IpAddress).HasMaxLength(45);
        builder.Property(x => x.PrinterName).HasMaxLength(100);
        builder.Property(x => x.PrintCopy).HasDefaultValue((short)1);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_printer_type",
            "type IN ('KITCHEN', 'CASHIER')"));

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.Type);
        builder.HasIndex(x => x.KitchenStationId).HasDatabaseName("ix_printer_kitchen_station");
        builder.HasIndex(x => x.CounterId).HasDatabaseName("ix_printer_counter");

        builder.HasOne(x => x.KitchenStation)
            .WithMany(x => x.Printers)
            .HasForeignKey(x => x.KitchenStationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Counter)
            .WithMany()
            .HasForeignKey(x => x.CounterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
