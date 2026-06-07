namespace Rpom.Application.Abstraction.Pricing;

/// <summary>
///     Refreshes ONLY PaidAmount + RefundAmount from SUCCESS payment rows.
///     No price recompute (AddTicketPayment doesn't change pricing — spec §5).
///     Does NOT SaveChanges.
/// </summary>
public interface IRefreshPaymentTotalsService
{
    Task RefreshAsync(long ticketId, CancellationToken ct);
}
