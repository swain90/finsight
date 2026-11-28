using FinSight.Application.Features.Imports.Common;
using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Imports.Commands.CreateImport;

public sealed record CreateImportCommand(string FileName, long Length, Stream Content, Guid? AccountId)
    : IRequest<Result<ImportBatchDto>>;
