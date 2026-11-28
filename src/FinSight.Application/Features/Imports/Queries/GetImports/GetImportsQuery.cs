using FinSight.Application.Features.Imports.Common;
using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Imports.Queries.GetImports;

public sealed record GetImportsQuery : IRequest<Result<IReadOnlyList<ImportBatchSummaryDto>>>;
