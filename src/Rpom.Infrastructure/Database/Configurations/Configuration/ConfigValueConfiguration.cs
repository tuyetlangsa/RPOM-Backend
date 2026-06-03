using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Configuration;

namespace Rpom.Infrastructure.Database.Configurations.Configuration;

internal sealed class ConfigValueConfiguration : IEntityTypeConfiguration<ConfigValue>
{
    public void Configure(EntityTypeBuilder<ConfigValue> builder)
    {
        builder.ToTable("config_values");

        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasMaxLength(100).ValueGeneratedNever();
        builder.Property(x => x.Value).HasColumnType("text");
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        // No FK on UpdatedByStaffAccountId — soft ref (like AuditLog).
    }
}
