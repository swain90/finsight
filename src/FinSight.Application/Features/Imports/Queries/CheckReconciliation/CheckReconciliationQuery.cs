using FinSight.Application.Features.Imports.Common;
using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Imports.Queries.CheckReconciliation;

public sealed record CheckReconciliationQuery(Guid ImportId, Guid AccountId, decimal ClosingBalance)
    : IRequest<Result<ReconciliationResult>>;
