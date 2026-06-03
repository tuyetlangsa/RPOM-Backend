using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Access;

namespace Rpom.Infrastructure.Database.Configurations.Access;

internal sealed class StaffAccountConfiguration : IEntityTypeConfiguration<StaffAccount>
{
    public void Configure(EntityTypeBuilder<StaffAccount> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Username).IsRequired().HasMaxLength(50);
        builder.Property(x => x.PasswordHash).IsRequired().HasMaxLength(255);
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.IsLocked).HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(x => x.Username).IsUnique();
        builder.HasIndex(x => x.RoleId);
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => new { x.IsActive, x.IsLocked }).HasDatabaseName("ix_staff_account_login");

        builder.HasOne(x => x.Role)
            .WithMany(x => x.StaffAccounts)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
