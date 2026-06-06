using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Configuration;

namespace Rpom.Infrastructure.Database.Configurations.Configuration;

internal sealed class RoundingConfigConfiguration : IEntityTypeConfiguration<RoundingConfig>
{
    public void Configure(EntityTypeBuilder<RoundingConfig> builder)
    {
        builder.HasKey(x => x.KeyCode);
        builder.Property(x => x.KeyCode).HasMaxLength(50);
        builder.Property(x => x.Digits).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(200);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
    }
}
