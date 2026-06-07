namespace Rpom.Application.Abstraction.Pricing;

/// <summary>
/// Eager full recompute of a ticket: re-snapshots percents onto each non-cancelled
/// OrderItem, recomputes line money, rebuilds TicketItemSum buckets, rolls up the
/// Ticket header. Does NOT SaveChanges — caller owns the transaction.
/// </summary>
public interface ITicketRecomputeService
{
    Task RecomputeAsync(long ticketId, CancellationToken ct);
}
