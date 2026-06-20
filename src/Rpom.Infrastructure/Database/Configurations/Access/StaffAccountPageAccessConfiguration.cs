using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Access;

namespace Rpom.Infrastructure.Database.Configurations.Access;

internal sealed class StaffAccountPageAccessConfiguration : IEntityTypeConfiguration<StaffAccountPageAccess>
{
    public void Configure(EntityTypeBuilder<StaffAccountPageAccess> builder)
    {
        builder.HasKey(x => new { x.StaffAccountId, x.PageId });

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(x => x.PageId).HasDatabaseName("ix_staff_account_page_access_page_id");

        builder.HasOne(x => x.StaffAccount)
            .WithMany(x => x.StaffAccountPageAccesses)
            .HasForeignKey(x => x.StaffAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Page)
            .WithMany(x => x.StaffAccountPageAccesses)
            .HasForeignKey(x => x.PageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
