using FinSight.Application.Features.Imports.Common;
using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Imports.Queries.GetImport;

public sealed record GetImportQuery(Guid ImportId) : IRequest<Result<ImportBatchDto>>;
