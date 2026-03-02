using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Transactions.Commands.RunAnomalyDetection;

public sealed record RunAnomalyDetectionCommand(Guid UserId) : IRequest<Result>;
