using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Restaurant;

namespace Rpom.Infrastructure.Database.Configurations.Restaurant;

internal sealed class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Description).HasMaxLength(200);
        builder.Property(x => x.DisplayOrder).HasDefaultValue((short)0);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.Property(x => x.ServiceChargePercent).HasPrecision(5, 2).HasDefaultValue(0m);
        builder.Property(x => x.ServiceChargeVatPercent).HasPrecision(5, 2).HasDefaultValue(0m);

        builder.HasIndex(x => x.CounterId);
        builder.HasIndex(x => new { x.CounterId, x.IsActive }).HasDatabaseName("ix_area_counter_active");

        builder.HasOne(x => x.Counter)
            .WithMany(x => x.Areas)
            .HasForeignKey(x => x.CounterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
