using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinSight.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Description).IsRequired().HasMaxLength(500);
        builder.Property(t => t.MerchantName).HasMaxLength(200);
        builder.Property(t => t.Notes).HasMaxLength(1000);
        builder.Property(t => t.PlaidTransactionId).HasMaxLength(100);

        builder.Property(t => t.Kind).HasConversion<string>().HasMaxLength(30).HasDefaultValue(TransactionKind.Regular);
        builder.Property(t => t.ImportFingerprint).HasMaxLength(64);

        builder.OwnsOne(t => t.Amount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("Amount").HasPrecision(18, 2);
            money.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3);
        });

        builder.Ignore(t => t.DomainEvents);

        // Composite index for common queries: user transactions by date
        builder.HasIndex(t => new { t.AccountId, t.OccurredAt });
        builder.HasIndex(t => new { t.AccountId, t.Category });
        builder.HasIndex(t => new { t.AccountId, t.ImportFingerprint }).IsUnique().HasFilter("\"ImportFingerprint\" IS NOT NULL");
        builder.HasIndex(t => t.TransferGroupId);
        builder.HasIndex(t => t.ImportBatchId);
        builder.HasIndex(t => t.Kind);
        builder.HasIndex(t => t.PlaidTransactionId).IsUnique().HasFilter("\"PlaidTransactionId\" IS NOT NULL");
    }
}
