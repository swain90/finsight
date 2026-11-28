using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Transactions.Commands.ResolveTransfer;

/// <summary>Pairs a pending transfer with <paramref name="PartnerId"/>, or dismisses it when the partner is null.</summary>
public sealed record ResolveTransferCommand(Guid TransactionId, Guid? PartnerId) : IRequest<Result>;
