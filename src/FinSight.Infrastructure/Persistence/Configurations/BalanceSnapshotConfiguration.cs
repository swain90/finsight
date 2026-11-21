using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinSight.Infrastructure.Persistence.Configurations;

public class BalanceSnapshotConfiguration : IEntityTypeConfiguration<BalanceSnapshot>
{
    public void Configure(EntityTypeBuilder<BalanceSnapshot> builder)
    {
        builder.HasKey(s => s.Id);

        builder.OwnsOne(s => s.Balance, money =>
        {
            money.Property(m => m.Amount).HasColumnName("Balance").HasPrecision(18, 2);
            money.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3);
        });

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(s => s.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(s => s.DomainEvents);
        builder.HasIndex(s => new { s.AccountId, s.AsOf });
        builder.HasIndex(s => s.ImportBatchId);
    }
}
