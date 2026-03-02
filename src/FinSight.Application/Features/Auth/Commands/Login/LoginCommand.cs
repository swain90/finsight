using FinSight.Application.Common.Models;
using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Auth.Commands.Login;

public sealed record LoginCommand(string Email, string Password, string IpAddress) 
    : IRequest<Result<AuthResult>>;
