using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Access;

namespace Rpom.Infrastructure.Database.Configurations.Access;

internal sealed class PermissionGroupConfiguration : IEntityTypeConfiguration<PermissionGroup>
{
    public void Configure(EntityTypeBuilder<PermissionGroup> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.DisplayOrder).HasDefaultValue((short)0);

        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasMany(x => x.Permissions)
            .WithOne(x => x.PermissionGroup)
            .HasForeignKey(x => x.PermissionGroupId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
