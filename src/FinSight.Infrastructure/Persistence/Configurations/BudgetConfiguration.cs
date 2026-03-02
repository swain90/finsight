using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinSight.Infrastructure.Persistence.Configurations;

public class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.HasKey(b => b.Id);

        builder.OwnsOne(b => b.Limit, money =>
        {
            money.Property(m => m.Amount).HasColumnName("LimitAmount").HasPrecision(18, 2);
            money.Property(m => m.Currency).HasColumnName("LimitCurrency").HasMaxLength(3);
        });

        builder.OwnsOne(b => b.Spent, money =>
        {
            money.Property(m => m.Amount).HasColumnName("SpentAmount").HasPrecision(18, 2);
            money.Property(m => m.Currency).HasColumnName("SpentCurrency").HasMaxLength(3);
        });

        builder.Ignore(b => b.DomainEvents);
        builder.Ignore(b => b.SpendingPercentage);
        builder.Ignore(b => b.Remaining);
        builder.Ignore(b => b.IsOverBudget);

        // Unique constraint: one budget per category per month/year per user
        builder.HasIndex(b => new { b.UserId, b.Category, b.Month, b.Year }).IsUnique();
    }
}
