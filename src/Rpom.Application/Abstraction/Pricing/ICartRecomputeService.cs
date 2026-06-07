namespace Rpom.Application.Abstraction.Pricing;

/// <summary>
///     Recomputes every CartItem of a DRAFT order in-place (eager, same transaction).
///     Does NOT call SaveChangesAsync — the caller's handler owns the unit of work.
/// </summary>
public interface ICartRecomputeService
{
    Task RecomputeAsync(long orderId, CancellationToken ct);
}
