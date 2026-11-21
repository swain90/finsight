using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Reporting;
using FinSight.Domain.Common;
using FinSight.Domain.Repositories;
using MediatR;

namespace FinSight.Application.Features.Budgets.Queries.GetBudgets;

public sealed class GetBudgetsQueryHandler(
    IBudgetRepository budgetRepository,
    ITransactionRepository transactionRepository,
    ICurrentUserService currentUserService) : IRequestHandler<GetBudgetsQuery, Result<IReadOnlyList<BudgetDto>>>
{
    public async Task<Result<IReadOnlyList<BudgetDto>>> Handle(GetBudgetsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure<IReadOnlyList<BudgetDto>>(Error.Unauthorized);

        var budgets = await budgetRepository.GetByUserIdAsync(
            currentUserService.UserId.Value, request.Month, request.Year, cancellationToken);

        var from = new DateTimeOffset(request.Year, request.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddMonths(1).AddTicks(-1);
        var transactions = await transactionRepository.GetByUserIdAsync(
            currentUserService.UserId.Value,
            from,
            to,
            cancellationToken);

        var spentByCategoryAndCurrency = transactions
            .Where(PersonalLedger.IsPersonalSpending)
            .GroupBy(t => (Category: t.Category, Currency: t.Amount.Currency))
            .ToDictionary(g => g.Key, g => g.Sum(PersonalLedger.SpendingAmount));

        var dtos = budgets
            .Select(b =>
            {
                var spentAmount = spentByCategoryAndCurrency.TryGetValue((b.Category, b.Limit.Currency), out var spent)
                    ? spent
                    : 0m;
                var remainingAmount = Math.Max(0, b.Limit.Amount - spentAmount);
                var spendingPercentage = b.Limit.Amount == 0
                    ? 0
                    : Math.Round(Math.Min(100m, (spentAmount / b.Limit.Amount) * 100m), 1);

                return new BudgetDto(
                    b.Id,
                    b.Category.ToString(),
                    b.Limit.Amount,
                    spentAmount,
                    remainingAmount,
                    b.Limit.Currency,
                    spendingPercentage,
                    spentAmount > b.Limit.Amount,
                    b.Month,
                    b.Year);
            })
            .OrderBy(b => b.Category)
            .ToList()
            .AsReadOnly();

        return Result.Success<IReadOnlyList<BudgetDto>>(dtos);
    }
}
