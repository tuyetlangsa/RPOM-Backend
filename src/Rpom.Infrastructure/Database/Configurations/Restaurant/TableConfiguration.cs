using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Restaurant;

namespace Rpom.Infrastructure.Database.Configurations.Restaurant;

internal sealed class TableConfiguration : IEntityTypeConfiguration<Table>
{
    public void Configure(EntityTypeBuilder<Table> builder)
    {
        // "Table" is a SQL reserved keyword in many engines; force the singular plural
        // form to "tables" (Postgres tolerates it lowercase; naming convention pluralizes too).
        builder.ToTable("tables");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(20);
        builder.Property(x => x.SeatCount).HasDefaultValue(1);
        builder.Property(x => x.Description).HasMaxLength(200);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue(TableStatus.Available);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_table_status",
            "status IN ('AVAILABLE', 'OCCUPIED')"));

        builder.HasIndex(x => x.AreaId);
        builder.HasIndex(x => new { x.AreaId, x.UpdatedAt }).HasDatabaseName("ix_table_area_updated");
        builder.HasIndex(x => new { x.AreaId, x.Code }).IsUnique().HasDatabaseName("ux_table_area_code");

        builder.HasOne(x => x.Area)
            .WithMany(x => x.Tables)
            .HasForeignKey(x => x.AreaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
