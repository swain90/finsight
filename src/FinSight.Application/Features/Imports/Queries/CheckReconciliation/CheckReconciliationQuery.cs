using FinSight.Application.Features.Imports.Common;
using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Imports.Queries.CheckReconciliation;

/// <param name="ImportId">The batch being reviewed.</param>
/// <param name="AccountId">The account whose statement is being reconciled.</param>
/// <param name="ClosingBalance">The statement's closing balance.</param>
/// <param name="AsOf">The statement's closing date; when null, the batch's last row date is used.</param>
public sealed record CheckReconciliationQuery(Guid ImportId, Guid AccountId, decimal ClosingBalance, DateOnly? AsOf = null)
    : IRequest<Result<ReconciliationResult>>;
