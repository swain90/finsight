using FinSight.Application.Common.Models;
using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Auth.Commands.Register;

public sealed record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName
) : IRequest<Result<AuthResult>>;
