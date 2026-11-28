using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Reporting;
using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using MediatR;

namespace FinSight.Application.Features.Reports.Queries.GetPersonalIncome;

public sealed class GetPersonalIncomeQueryHandler(
    ITransactionRepository transactionRepository,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider) : IRequestHandler<GetPersonalIncomeQuery, Result<PersonalIncomeDto>>
{
    public async Task<Result<PersonalIncomeDto>> Handle(GetPersonalIncomeQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure<PersonalIncomeDto>(Error.Unauthorized);

        var year = request.Year ?? timeProvider.GetUtcNow().UtcDateTime.Year;
        if (year is < 1900 or > 2200)
            return Result.Failure<PersonalIncomeDto>(Error.Custom("Reports.InvalidYear", "Year is out of range."));

        var from = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddYears(1);
        var transactions = (await transactionRepository.GetByUserIdAsync(currentUserService.UserId.Value, from, to, cancellationToken))
            .Where(t => t.OccurredAt >= from && t.OccurredAt < to)
            .ToList();

        var pay = transactions.Where(t => t.Kind == TransactionKind.OwnerPay && PersonalLedger.IsTaxableIncome(t)).Sum(t => t.Amount.Amount);
        var taxable = transactions.Where(PersonalLedger.IsTaxableIncome).Sum(t => t.Amount.Amount);
        var sheltered = transactions.Where(PersonalLedger.IsTaxShelteredIncome).Sum(t => t.Amount.Amount);

        return Result.Success(new PersonalIncomeDto(year, pay, taxable - pay, sheltered));
    }
}
