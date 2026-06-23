using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Operations;

namespace Rpom.Infrastructure.Database.Configurations.Operations;

internal sealed class CustomerDisplayConfiguration : IEntityTypeConfiguration<CustomerDisplay>
{
    public void Configure(EntityTypeBuilder<CustomerDisplay> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.DeviceToken).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ActivatedClientId).HasMaxLength(64);
        builder.Property(x => x.IdleMediaUrl).HasMaxLength(500);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(x => x.DeviceToken).IsUnique();
        builder.HasIndex(x => x.PosTerminalId).IsUnique();

        builder.HasOne(x => x.PosTerminal)
            .WithMany()
            .HasForeignKey(x => x.PosTerminalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
