using FinSight.Application.Features.Imports.Common;
using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using MediatR;

namespace FinSight.Application.Features.Imports.Commands.UpdateImportRow;

public sealed record UpdateImportRowCommand(
    Guid ImportId,
    Guid RowId,
    TransactionCategory? Category,
    bool? IsExcluded,
    Guid? AccountId) : IRequest<Result<ImportRowDto>>;
