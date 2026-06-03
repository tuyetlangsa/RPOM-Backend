using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Menu;

namespace Rpom.Infrastructure.Database.Configurations.Menu;

internal sealed class ModifierConfiguration : IEntityTypeConfiguration<Modifier>
{
    public void Configure(EntityTypeBuilder<Modifier> builder)
    {
        builder.HasKey(x => new { x.ChoiceCategoryId, x.ItemId });

        builder.Property(x => x.ExtraPrice).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.MinPerModifier).HasDefaultValue(0);
        builder.Property(x => x.MaxPerModifier).HasDefaultValue(1);
        builder.Property(x => x.DisplayOrder).HasDefaultValue((short)0);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(x => x.ItemId).HasDatabaseName("ix_modifier_item");

        builder.HasOne(x => x.ChoiceCategory)
            .WithMany(x => x.Modifiers)
            .HasForeignKey(x => x.ChoiceCategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Item)
            .WithMany(x => x.ModifierRoles)
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
