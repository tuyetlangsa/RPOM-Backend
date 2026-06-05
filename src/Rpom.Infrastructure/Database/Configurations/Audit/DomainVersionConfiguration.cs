using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Common;

namespace Rpom.Infrastructure.Database.Configurations.Audit;

internal sealed class DomainVersionConfiguration : IEntityTypeConfiguration<DomainVersion>
{
    public void Configure(EntityTypeBuilder<DomainVersion> builder)
    {
        builder.HasKey(x => x.Scope);

        builder.Property(x => x.Scope).HasMaxLength(50);
        builder.Property(x => x.Version).HasDefaultValue(0L);
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedBySource).HasMaxLength(100);
    }
}
