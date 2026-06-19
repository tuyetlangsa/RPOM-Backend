using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Operations;

namespace Rpom.Infrastructure.Database.Configurations.Operations;

internal sealed class NotificationReadStateConfiguration : IEntityTypeConfiguration<NotificationReadState>
{
    public void Configure(EntityTypeBuilder<NotificationReadState> builder)
    {
        builder.HasKey(x => new { x.StaffAccountId, x.CounterId });

        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasOne(x => x.StaffAccount)
            .WithMany()
            .HasForeignKey(x => x.StaffAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Counter)
            .WithMany()
            .HasForeignKey(x => x.CounterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
