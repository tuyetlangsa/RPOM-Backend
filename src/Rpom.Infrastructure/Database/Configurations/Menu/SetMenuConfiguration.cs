using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Menu;

namespace Rpom.Infrastructure.Database.Configurations.Menu;

internal sealed class SetMenuConfiguration : IEntityTypeConfiguration<SetMenu>
{
    public void Configure(EntityTypeBuilder<SetMenu> builder)
    {
        // 1:1 specialization: PK = FK = ItemId
        builder.HasKey(x => x.ItemId);

        builder.Property(x => x.ItemId).ValueGeneratedNever();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasOne(x => x.Item)
            .WithOne(x => x.SetMenu)
            .HasForeignKey<SetMenu>(x => x.ItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
