using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Persistence.Repositories;

public sealed class BudgetRepository(ApplicationDbContext context) : IBudgetRepository
{
    public async Task<Budget?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await context.Budgets.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Budget>> GetByUserIdAsync(Guid userId, int month, int year, CancellationToken cancellationToken = default)
        => await context.Budgets
            .Where(b => b.UserId == userId && b.Month == month && b.Year == year)
            .OrderBy(b => b.Category)
            .ToListAsync(cancellationToken);

    public async Task<Budget?> GetByCategoryAsync(Guid userId, TransactionCategory category, int month, int year, CancellationToken cancellationToken = default)
        => await context.Budgets
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Category == category
                && b.Month == month && b.Year == year, cancellationToken);

    public async Task AddAsync(Budget budget, CancellationToken cancellationToken = default)
        => await context.Budgets.AddAsync(budget, cancellationToken);

    public Task UpdateAsync(Budget budget, CancellationToken cancellationToken = default)
    {
        context.Budgets.Update(budget);
        return Task.CompletedTask;
    }
}
