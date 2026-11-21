using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinSight.Infrastructure.Persistence.Configurations;

public class MerchantRuleConfiguration : IEntityTypeConfiguration<MerchantRule>
{
    public void Configure(EntityTypeBuilder<MerchantRule> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.DescriptionKey).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Category).HasConversion<string>().HasMaxLength(50);

        builder.Ignore(r => r.DomainEvents);
        builder.HasIndex(r => new { r.UserId, r.DescriptionKey }).IsUnique();
    }
}
