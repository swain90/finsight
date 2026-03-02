using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using FinSight.Domain.ValueObjects;
using MediatR;

namespace FinSight.Application.Features.Transactions.Commands.RecordTransaction;

public sealed class RecordTransactionCommandHandler(
    IAccountRepository accountRepository,
    IBudgetRepository budgetRepository,
    ICurrentUserService currentUserService,
    IApplicationDbContext applicationDbContext,
    IUnitOfWork unitOfWork) : IRequestHandler<RecordTransactionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RecordTransactionCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure<Guid>(Error.Unauthorized);

        var ownsAccount = await accountRepository.BelongsToUserAsync(
            request.AccountId, currentUserService.UserId.Value, cancellationToken);

        if (!ownsAccount)
            return Result.Failure<Guid>(Error.Unauthorized);

        var account = await accountRepository.GetByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
            return Result.Failure<Guid>(Error.NotFound);

        var money = new Money(request.Amount, request.Currency);
        var normalizedCategory = NormalizeLegacyCategory(request.Category);
        var transaction = account.RecordTransaction(
            request.Description, money, request.Type,
            request.OccurredAt, normalizedCategory, request.MerchantName);

        // Debit transactions contribute to budget spend for the matching month/category.
        if (request.Type == TransactionType.Debit)
        {
            var budget = await budgetRepository.GetByCategoryAsync(
                currentUserService.UserId.Value,
                normalizedCategory,
                request.OccurredAt.Month,
                request.OccurredAt.Year,
                cancellationToken);

            if (budget is not null && budget.Limit.Currency == money.Currency)
            {
                budget.AddSpending(money);
                await budgetRepository.UpdateAsync(budget, cancellationToken);
            }
        }

        // Ensure EF persists this as INSERT, not graph-inferred UPDATE.
        await applicationDbContext.Transactions.AddAsync(transaction, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(transaction.Id);
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
