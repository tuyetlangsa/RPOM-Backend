using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Reservation;

namespace Rpom.Infrastructure.Database.Configurations.Reservation;

internal sealed class ReservationTableConfiguration : IEntityTypeConfiguration<ReservationTable>
{
    public void Configure(EntityTypeBuilder<ReservationTable> builder)
    {
        builder.HasKey(x => new { x.ReservationId, x.TableId });

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(x => x.TableId).HasDatabaseName("ix_reservation_table_table_id");

        builder.HasOne(x => x.Table)
            .WithMany()
            .HasForeignKey(x => x.TableId)
            .OnDelete(DeleteBehavior.Restrict);
        // Reservation side configured via ReservationConfiguration.HasMany(...).
    }
}
