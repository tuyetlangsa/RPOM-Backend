using System.Reflection;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rpom.Application.Abstraction.Exceptions;
using Rpom.Domain.Common;

namespace Rpom.Application.Abstraction.Behaviors;

internal sealed class ExceptionHandlingPipelineBehavior<TRequest, TResponse>(
    ILogger<ExceptionHandlingPipelineBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
{
    private static readonly Error ConcurrencyConflict =
        Error.Conflict("Concurrency.Conflict", "Data was modified by another operation. Please refresh and retry.");

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "Concurrency conflict for {RequestName}", typeof(TRequest).Name);

            if (typeof(TResponse).IsGenericType &&
                typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
            {
                Type resultType = typeof(TResponse).GetGenericArguments()[0];
                MethodInfo? failureMethod = typeof(Result<>)
                    .MakeGenericType(resultType)
                    .GetMethod(nameof(Result<object>.Failure))!;
                return (TResponse)failureMethod.Invoke(null, [ConcurrencyConflict])!;
            }

            if (typeof(TResponse) == typeof(Result))
                return (TResponse)(object)Result.Failure(ConcurrencyConflict);

            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception for {RequestName}", typeof(TRequest).Name);

            throw new RpomException(typeof(TRequest).Name, innerException: exception);
        }
    }
}
