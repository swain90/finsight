using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinSight.Infrastructure.Persistence.Configurations;

public class ImportRowConfiguration : IEntityTypeConfiguration<ImportRow>
{
    public void Configure(EntityTypeBuilder<ImportRow> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Description).IsRequired().HasMaxLength(500);
        builder.Property(r => r.MerchantName).HasMaxLength(200);
        builder.Property(r => r.Fingerprint).HasMaxLength(64);
        builder.Property(r => r.RawLine).IsRequired().HasMaxLength(1000);
        builder.Property(r => r.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.Category).HasConversion<string>().HasMaxLength(50);
        builder.Property(r => r.KindHint).HasConversion<string>().HasMaxLength(30);

        builder.OwnsOne(r => r.Amount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("Amount").HasPrecision(18, 2);
            money.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3);
        });

        builder.Ignore(r => r.DomainEvents);
        builder.HasIndex(r => r.ImportBatchId);
    }
}
