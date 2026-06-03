using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Menu;

namespace Rpom.Infrastructure.Database.Configurations.Menu;

internal sealed class SetMenuDetailConfiguration : IEntityTypeConfiguration<SetMenuDetail>
{
    public void Configure(EntityTypeBuilder<SetMenuDetail> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DetailType).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.DisplayOrder).HasDefaultValue((short)0);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_set_menu_detail_type",
            "detail_type IN ('COMPONENT', 'CHOICE_CATEGORY')"));

        builder.HasIndex(x => x.SetMenuItemId);
        builder.HasIndex(x => new { x.SetMenuItemId, x.DisplayOrder }).HasDatabaseName("ix_set_menu_detail_order");
        builder.HasIndex(x => x.ComponentItemId).HasDatabaseName("ix_set_menu_detail_component");
        builder.HasIndex(x => x.ChoiceCategoryId).HasDatabaseName("ix_set_menu_detail_choice_category");

        builder.HasOne(x => x.SetMenu)
            .WithMany(x => x.Details)
            .HasForeignKey(x => x.SetMenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ComponentItem)
            .WithMany()
            .HasForeignKey(x => x.ComponentItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ChoiceCategory)
            .WithMany()
            .HasForeignKey(x => x.ChoiceCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
