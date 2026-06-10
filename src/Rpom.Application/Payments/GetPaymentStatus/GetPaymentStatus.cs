using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;

namespace Rpom.Application.Payments.GetPaymentStatus;
public static class GetPaymentStatus
{
    public sealed record Query(long PaymentId) : IQuery<Response>;

    public sealed record Response(
        long PaymentId,
        long TicketId,
        string PaymentMethodCode,
        decimal Amount,
        string Status,
        string? TransactionRef,
        DateTime? ProcessedAt,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        decimal TicketTotalAmount,
        decimal TicketPaidAmount,
        decimal RemainingAmount,
        decimal RefundAmount,
        bool IsFullyPaid);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            var row = await dbContext.TicketPaymentDetails
                .AsNoTracking()
                .Where(p => p.Id == request.PaymentId)
                .Select(p => new
                {
                    p.Id,
                    p.TicketId,
                    MethodCode = p.PaymentMethod.Code,
                    p.Amount,
                    p.Status,
                    p.TransactionRef,
                    p.ProcessedAt,
                    p.CreatedAt,
                    p.UpdatedAt,
                    TicketTotal = p.Ticket.TotalAmount,
                    TicketPaid = p.Ticket.PaidAmount,
                    TicketRefund = p.Ticket.RefundAmount
                })
                .FirstOrDefaultAsync(ct);

            if (row is null)
                return Result.Failure<Response>(PaymentErrors.PaymentNotFound);

            var remaining = row.TicketTotal - row.TicketPaid;

            return Result.Success(new Response(
                row.Id,
                row.TicketId,
                row.MethodCode,
                row.Amount,
                row.Status,
                row.TransactionRef,
                row.ProcessedAt,
                row.CreatedAt,
                row.UpdatedAt,
                row.TicketTotal,
                row.TicketPaid,
                remaining < 0 ? 0 : remaining,
                row.TicketRefund,
                remaining <= 0));
        }
    }
}
