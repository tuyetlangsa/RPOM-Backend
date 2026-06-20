using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Access;

namespace Rpom.Infrastructure.Database.Configurations.Access;

internal sealed class PageConfiguration : IEntityTypeConfiguration<Page>
{
    public void Configure(EntityTypeBuilder<Page> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DisplayOrder).HasDefaultValue((short)0);

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.ModuleId);

        builder.HasOne(x => x.Module)
            .WithMany(x => x.Pages)
            .HasForeignKey(x => x.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
