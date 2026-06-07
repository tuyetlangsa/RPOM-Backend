using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Domain.Sales;

namespace Rpom.Infrastructure.Pricing;

internal sealed class RefreshPaymentTotalsService(IDbContext dbContext, IDateTimeProvider clock)
    : IRefreshPaymentTotalsService
{
    public async Task RefreshAsync(long ticketId, CancellationToken ct)
    {
        Ticket ticket = await dbContext.Tickets.FirstAsync(t => t.Id == ticketId, ct);

        decimal paid = await dbContext.TicketPaymentDetails
            .Where(p => p.TicketId == ticketId && p.Status == TicketPaymentStatus.Success)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        ticket.PaidAmount = paid;
        ticket.RefundAmount = Math.Max(0m, paid - ticket.TotalAmount);
        ticket.UpdatedAt = clock.UtcNow;
    }
}
