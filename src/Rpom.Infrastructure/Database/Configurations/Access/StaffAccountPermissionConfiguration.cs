using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Access;

namespace Rpom.Infrastructure.Database.Configurations.Access;

internal sealed class StaffAccountPermissionConfiguration : IEntityTypeConfiguration<StaffAccountPermission>
{
    public void Configure(EntityTypeBuilder<StaffAccountPermission> builder)
    {
        builder.HasKey(x => new { x.StaffAccountId, x.PermissionId });

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(x => x.PermissionId).HasDatabaseName("ix_staff_account_permission_permission_id");

        builder.HasOne(x => x.StaffAccount)
            .WithMany(x => x.StaffAccountPermissions)
            .HasForeignKey(x => x.StaffAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Permission)
            .WithMany(x => x.StaffAccountPermissions)
            .HasForeignKey(x => x.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
