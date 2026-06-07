using MediatR;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;

namespace Rpom.Application.Abstraction.Behaviors;

/// <summary>
/// Wraps every <see cref="IBaseCommand"/> in a single database transaction so that
/// handlers with multiple <c>SaveChangesAsync</c> calls remain atomic — all-or-nothing.
/// Runs innermost (closest to the handler), after validation.
/// </summary>
internal sealed class TransactionPipelineBehavior<TRequest, TResponse>(
    IDbContext dbContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var response = await next();
            await tx.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
