using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinSight.Infrastructure.Persistence.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
        builder.Property(a => a.InstitutionName).HasMaxLength(200);
        builder.Property(a => a.PlaidAccountId).HasMaxLength(100);

        // Money value object as owned type
        builder.OwnsOne(a => a.Balance, money =>
        {
            money.Property(m => m.Amount).HasColumnName("Balance").HasPrecision(18, 2);
            money.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3);
        });

        builder.HasMany(a => a.Transactions)
            .WithOne(t => t.Account)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(a => a.DomainEvents);
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.PlaidAccountId).IsUnique().HasFilter("\"PlaidAccountId\" IS NOT NULL");
    }
}
