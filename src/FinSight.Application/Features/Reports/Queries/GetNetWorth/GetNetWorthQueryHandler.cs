using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Reporting;
using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using FinSight.Domain.ValueObjects;
using MediatR;

namespace FinSight.Application.Features.Reports.Queries.GetNetWorth;

public sealed class GetNetWorthQueryHandler(
    IAccountRepository accountRepository,
    IBalanceSnapshotRepository snapshotRepository,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider) : IRequestHandler<GetNetWorthQuery, Result<NetWorthDto>>
{
    public const int MaxMonths = 120;

    public async Task<Result<NetWorthDto>> Handle(GetNetWorthQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure<NetWorthDto>(Error.Unauthorized);
        if (request.Months is < 1 or > MaxMonths)
            return Result.Failure<NetWorthDto>(Error.Custom("Reports.InvalidMonths", $"Months must be between 1 and {MaxMonths}."));
        var userId = currentUserService.UserId.Value;

        var accounts = (await accountRepository.GetByUserIdAsync(userId, cancellationToken)).Where(a => a.IsActive).ToList();
        var snapshots = (await snapshotRepository.GetByUserIdAsync(userId, cancellationToken))
            .GroupBy(s => s.AccountId)
            .ToDictionary(g => g.Key, g => g.Select(s => (s.AsOf, s.Balance.Amount)).ToList());

        IReadOnlyList<AccountBalanceSeries> series = accounts
            .Select(a => new AccountBalanceSeries(a.Id, a.Owner, a.Type.IsLiability(), a.Balance.Amount,
                snapshots.TryGetValue(a.Id, out var s) ? s : []))
            .ToList();

        var (personal, including) = NetWorthCalculator.Current(series);

        // Month-ends of the last N months; the last point is the current month's end (may be in the future).
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var history = new List<NetWorthPointDto>();
        for (var i = request.Months - 1; i >= 0; i--)
        {
            var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-i);
            var monthEnd = new DateOnly(first.Year, first.Month, DateTime.DaysInMonth(first.Year, first.Month));
            if (NetWorthCalculator.At(monthEnd, series) is { } point)
                history.Add(new NetWorthPointDto(monthEnd, point.Personal, point.IncludingCorporation));
        }

        var currency = accounts.Select(a => a.Balance.Currency).FirstOrDefault() ?? Money.DefaultCurrency;
        return Result.Success(new NetWorthDto(personal, including, currency, history));
    }
}
