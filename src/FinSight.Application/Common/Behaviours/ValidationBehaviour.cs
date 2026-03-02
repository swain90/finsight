using FluentValidation;
using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Common.Behaviours;

public sealed class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next();

        // If response is a Result<T>, return failure result instead of throwing
        if (typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var errorMessage = string.Join("; ", failures.Select(f => f.ErrorMessage));
            var error = Error.Custom("Validation.Failed", errorMessage);

            var failureMethod = typeof(Result).GetMethod(nameof(Result.Failure), 1, [typeof(Error)])!
                .MakeGenericMethod(typeof(TResponse).GetGenericArguments()[0]);

            return (TResponse)failureMethod.Invoke(null, [error])!;
        }

        throw new ValidationException(failures);
    }
}
