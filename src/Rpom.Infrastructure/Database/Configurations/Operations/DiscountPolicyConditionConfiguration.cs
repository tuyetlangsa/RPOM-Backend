using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rpom.Domain.Operations;

namespace Rpom.Infrastructure.Database.Configurations.Operations;

internal sealed class DiscountPolicyConditionConfiguration : IEntityTypeConfiguration<DiscountPolicyCondition>
{
    public void Configure(EntityTypeBuilder<DiscountPolicyCondition> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ThresholdAmount).HasPrecision(18, 2);
        builder.Property(x => x.QuantityThreshold).HasPrecision(18, 3);
        builder.Property(x => x.ApplyType).IsRequired().HasMaxLength(10);
        builder.Property(x => x.DiscountValue).HasPrecision(18, 2);
        builder.Property(x => x.DisplayOrder).HasDefaultValue((short)0);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_discount_policy_condition_apply_type",
            "apply_type IN ('PERCENT', 'FIXED')"));

        builder.HasIndex(x => x.DiscountPolicyId);
        builder.HasIndex(x => x.ItemId).HasDatabaseName("ix_discount_policy_condition_item");
        builder.HasIndex(x => x.AreaId).HasDatabaseName("ix_discount_policy_condition_area");

        builder.HasOne(x => x.DiscountPolicy)
            .WithMany(x => x.Conditions)
            .HasForeignKey(x => x.DiscountPolicyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Area)
            .WithMany()
            .HasForeignKey(x => x.AreaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
