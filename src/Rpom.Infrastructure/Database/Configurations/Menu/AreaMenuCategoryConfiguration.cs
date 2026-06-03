using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Menu;

namespace Rpom.Infrastructure.Database.Configurations.Menu;

internal sealed class AreaMenuCategoryConfiguration : IEntityTypeConfiguration<AreaMenuCategory>
{
    public void Configure(EntityTypeBuilder<AreaMenuCategory> builder)
    {
        builder.HasKey(x => new { x.AreaId, x.CategoryId });

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(x => x.CategoryId).HasDatabaseName("ix_area_menu_category_category");

        builder.HasOne(x => x.Area)
            .WithMany()
            .HasForeignKey(x => x.AreaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
