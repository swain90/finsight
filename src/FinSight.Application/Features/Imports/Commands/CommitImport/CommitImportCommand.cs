using FinSight.Application.Features.Imports.Common;
using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Imports.Commands.CommitImport;

/// <summary>The closing balance the statement reports for one account.</summary>
public sealed record ClosingBalanceInput(Guid AccountId, decimal ClosingBalance, DateOnly AsOf);

public sealed record CommitImportCommand(Guid ImportId, IReadOnlyList<ClosingBalanceInput> Balances)
    : IRequest<Result<ImportBatchDto>>;
