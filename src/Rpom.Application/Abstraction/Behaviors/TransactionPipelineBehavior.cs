using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;

namespace Rpom.Application.Abstraction.Behaviors;

/// <summary>
///     Wraps every <see cref="IBaseCommand" /> in a single database transaction so that
///     handlers with multiple <c>SaveChangesAsync</c> calls remain atomic — all-or-nothing.
///     Uses <c>CreateExecutionStrategy</c> so it is compatible with Npgsql's retry-on-failure
///     policy. Runs innermost (closest to the handler), after validation.
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
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction tx =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                TResponse response = await next();
                await tx.CommitAsync(cancellationToken);
                return response;
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}
