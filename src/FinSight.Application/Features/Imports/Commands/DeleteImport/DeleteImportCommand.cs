using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Imports.Commands.DeleteImport;

/// <summary>Discards a pending import, or undoes a committed one.</summary>
public sealed record DeleteImportCommand(Guid ImportId) : IRequest<Result>;
