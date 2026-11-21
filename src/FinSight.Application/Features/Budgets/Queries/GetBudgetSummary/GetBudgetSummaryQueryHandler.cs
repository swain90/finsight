using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Reporting;
using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using MediatR;

namespace FinSight.Application.Features.Budgets.Queries.GetBudgetSummary;

public sealed class GetBudgetSummaryQueryHandler(
    IBudgetRepository budgetRepository,
    ITransactionRepository transactionRepository,
    ICurrentUserService currentUserService) : IRequestHandler<GetBudgetSummaryQuery, Result<BudgetSummaryDto>>
{
    public async Task<Result<BudgetSummaryDto>> Handle(GetBudgetSummaryQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure<BudgetSummaryDto>(Error.Unauthorized);

        var budgets = await budgetRepository.GetByUserIdAsync(
            currentUserService.UserId.Value, request.Month, request.Year, cancellationToken);

        if (!budgets.Any())
            return Result.Success(new BudgetSummaryDto(
                request.Month, request.Year, 0, 0, 0, 0, 0, 0, "CAD", 0, []));

        var from = new DateTimeOffset(request.Year, request.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddMonths(1).AddTicks(-1);
        var transactions = await transactionRepository.GetByUserIdAsync(
            currentUserService.UserId.Value,
            from,
            to,
            cancellationToken);

        var spentByCategoryAndCurrency = transactions
            .Where(PersonalLedger.IsPersonalSpending)
            .GroupBy(t => (Category: NormalizeLegacyCategory(t.Category), Currency: t.Amount.Currency))
            .ToDictionary(g => g.Key, g => g.Sum(PersonalLedger.SpendingAmount));

        var totalBudgeted = budgets.Sum(b => b.Limit.Amount);
        var totalSpent = budgets.Sum(b =>
            spentByCategoryAndCurrency.TryGetValue((b.Category, b.Limit.Currency), out var spentAmount)
                ? spentAmount
                : 0m);
        var currency = budgets.First().Limit.Currency;

        var overBudget = budgets
            .Where(b =>
            {
                var spentAmount = spentByCategoryAndCurrency.TryGetValue((b.Category, b.Limit.Currency), out var spent)
                    ? spent
                    : 0m;
                return spentAmount > b.Limit.Amount;
            })
            .ToList();
        var alerts = overBudget
            .Select(b =>
            {
                var spentAmount = spentByCategoryAndCurrency.TryGetValue((b.Category, b.Limit.Currency), out var spent)
                    ? spent
                    : 0m;
                var overAmount = spentAmount - b.Limit.Amount;
                var overPercentage = b.Limit.Amount == 0 ? 0 : Math.Round((overAmount / b.Limit.Amount) * 100, 1);

                return new OverBudgetAlertDto(
                    b.Category.ToString(),
                    b.Limit.Amount,
                    spentAmount,
                    overAmount,
                    overPercentage);
            })
            .ToList()
            .AsReadOnly();

        return Result.Success(new BudgetSummaryDto(
            request.Month, request.Year,
            budgets.Count,
            budgets.Count - overBudget.Count,
            overBudget.Count,
            totalBudgeted,
            totalSpent,
            totalBudgeted - totalSpent,
            currency,
            totalBudgeted > 0 ? Math.Round(totalSpent / totalBudgeted * 100, 1) : 0,
            alerts));
    }

    private static TransactionCategory NormalizeLegacyCategory(TransactionCategory category) => category switch
    {
        (TransactionCategory)1 => TransactionCategory.GroceriesAndFood,
        (TransactionCategory)2 => TransactionCategory.Transportation,
        (TransactionCategory)3 => TransactionCategory.HousingAndRent,
        (TransactionCategory)4 => TransactionCategory.Utilities,
        (TransactionCategory)5 => TransactionCategory.HealthAndMedical,
        (TransactionCategory)6 => TransactionCategory.Entertainment,
        (TransactionCategory)7 => TransactionCategory.ShoppingAndClothing,
        (TransactionCategory)8 => TransactionCategory.Salary,
        (TransactionCategory)9 => TransactionCategory.Other,
        (TransactionCategory)10 => TransactionCategory.Other,
        (TransactionCategory)11 => TransactionCategory.EducationAndWork,
        _ => category
    };
}
