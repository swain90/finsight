using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Reporting;
using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using MediatR;

namespace FinSight.Application.Features.Reports.Queries.GetCorporationSummary;

public sealed class GetCorporationSummaryQueryHandler(
    ITransactionRepository transactionRepository,
    IAccountRepository accountRepository,
    IBalanceSnapshotRepository snapshotRepository,
    IUserRepository userRepository,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider) : IRequestHandler<GetCorporationSummaryQuery, Result<CorporationSummaryDto>>
{
    public async Task<Result<CorporationSummaryDto>> Handle(GetCorporationSummaryQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure<CorporationSummaryDto>(Error.Unauthorized);
        var userId = currentUserService.UserId.Value;

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return Result.Failure<CorporationSummaryDto>(Error.Unauthorized);

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var endingYear = request.FiscalYear
            ?? FiscalYear.EndingYearContaining(today, user.CorporateFiscalYearEndMonth, user.CorporateFiscalYearEndDay);
        if (endingYear is < 1900 or > 2200)
            return Result.Failure<CorporationSummaryDto>(Error.Custom("Reports.InvalidFiscalYear", "Fiscal year is out of range."));
        var (start, end) = FiscalYear.Range(user.CorporateFiscalYearEndMonth, user.CorporateFiscalYearEndDay, endingYear);

        // Imported rows sit at UTC midnight of their date (R7); the range is inclusive of the end date.
        var from = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = new DateTimeOffset(end.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var transactions = (await transactionRepository.GetByUserIdAsync(userId, from, to, cancellationToken))
            .Where(t => t.Account is { Owner: AccountOwner.Corporation } && t.OccurredAt >= from && t.OccurredAt < to)
            .ToList();

        // R26: assets minus liabilities (stored as positive amounts owed), each from its latest snapshot on or
        // before the fiscal-year end. The running Balance stands in only for an account with no such snapshot,
        // and only while the fiscal year is current. R20: inactive accounts are included.
        var accounts = (await accountRepository.GetByUserIdAsync(userId, cancellationToken))
            .Where(a => a.Owner == AccountOwner.Corporation)
            .ToList();
        var corporateIds = accounts.Select(a => a.Id).ToHashSet();
        var snapshotsAtEnd = (await snapshotRepository.GetByUserIdAsync(userId, cancellationToken))
            .Where(s => corporateIds.Contains(s.AccountId) && s.AsOf <= end)
            .GroupBy(s => s.AccountId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.AsOf).ThenByDescending(s => s.CreatedAt).First());
        var isCurrentYear = start <= today && today <= end;
        decimal closing = 0m;
        foreach (var account in accounts)
        {
            decimal? balance = snapshotsAtEnd.TryGetValue(account.Id, out var snapshot)
                ? snapshot.Balance.Amount
                : isCurrentYear ? account.Balance.Amount : null;
            if (balance is { } amount)
                closing += account.Type.IsLiability() ? -amount : amount;
        }

        static decimal Revenue(IEnumerable<Domain.Entities.Transaction> ts) =>
            ts.Where(t => t.Kind == TransactionKind.Regular && t.Type == TransactionType.Credit).Sum(t => t.Amount.Amount);
        static decimal Paid(IEnumerable<Domain.Entities.Transaction> ts) =>
            ts.Where(t => t.Kind == TransactionKind.OwnerPay && t.Type == TransactionType.Debit).Sum(t => t.Amount.Amount);
        static decimal Expenses(IEnumerable<Domain.Entities.Transaction> ts) =>
            ts.Where(t => t.Kind == TransactionKind.Regular && t.Type == TransactionType.Debit).Sum(t => t.Amount.Amount);

        var months = new List<CorporationMonthDto>();
        for (var m = new DateOnly(start.Year, start.Month, 1); m <= end; m = m.AddMonths(1))
        {
            var inMonth = transactions.Where(t => t.OccurredAt.UtcDateTime.Year == m.Year && t.OccurredAt.UtcDateTime.Month == m.Month).ToList();
            months.Add(new CorporationMonthDto(m.Year, m.Month, Revenue(inMonth), Paid(inMonth), Expenses(inMonth)));
        }

        return Result.Success(new CorporationSummaryDto(start, end, Revenue(transactions), Paid(transactions),
            Expenses(transactions), closing, months));
    }
}
