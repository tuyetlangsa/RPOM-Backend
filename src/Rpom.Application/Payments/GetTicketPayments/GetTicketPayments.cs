using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;

namespace Rpom.Application.Payments.GetTicketPayments;
public static class GetTicketPayments
{
    public sealed record Query(long TicketId) : IQuery<Response>;

    public sealed record Response(
        long TicketId,
        string TicketCode,
        string TicketStatus,
        long TotalAmount,
        long PaidAmount,
        long RemainingAmount,
        long RefundAmount,
        bool IsFullyPaid,
        IReadOnlyList<PaymentLine> Payments);

    public sealed record PaymentLine(
        long Id,
        int PaymentMethodId,
        string PaymentMethodCode,
        string PaymentMethodName,
        decimal Amount,
        string Status,
        string? TransactionRef,
        string? Notes,
        long? ParentPaymentDetailId,  //null if it is a normal payment, not a refund linked to another payment
        int ProcessedByStaffId,
        DateTime? ProcessedAt,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        IReadOnlyList<TransactionLine> Transactions);  //empty "" with payment amount or QR code, no transaction has been made

    public sealed record TransactionLine(
        long Id,
        long GatewayTransactionId,
        decimal TransferAmount,
        string Status,
        string? Code,
        string? Content,
        string? ReferenceCode,
        long? MatchedPaymentDetailId,
        DateTime TransactionDate,
        DateTime CreatedAt);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            var ticket = await dbContext.Tickets
                .AsNoTracking()
                .Where(x => x.Id == request.TicketId)
                .Select(x => new
                {
                    x.Id, x.Code, x.Status,
                    x.TotalAmount, x.PaidAmount, x.RefundAmount
                })
                .FirstOrDefaultAsync(ct);

            if (ticket is null)
                return Result.Failure<Response>(PaymentErrors.TicketNotFound);

            // All payments except for those that have been soft-deleted (DELETED)
            var payments = await dbContext.TicketPaymentDetails
                .AsNoTracking()
                .Where(p => p.TicketId == request.TicketId && p.Status != TicketPaymentStatus.Deleted)
                .OrderBy(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id, p.PaymentMethodId,
                    PaymentMethodCode = p.PaymentMethod.Code,
                    PaymentMethodName = p.PaymentMethod.Name,
                    p.Amount, p.Status, p.TransactionRef, p.Notes, p.ParentPaymentDetailId,
                    p.ProcessedByStaffId, p.ProcessedAt, p.CreatedAt, p.UpdatedAt
                })
                .ToListAsync(ct);

            var paymentIds = payments.Select(p => p.Id).ToList();

            // Transation of QR payment
            var transactions = await dbContext.PaymentTransactions
                .AsNoTracking()
                .Where(t => t.MatchedPaymentDetailId != null
                            && paymentIds.Contains(t.MatchedPaymentDetailId.Value))
                .OrderBy(t => t.CreatedAt)
                .Select(t => new TransactionLine(
                    t.Id, t.GatewayTransactionId, t.TransferAmount, t.Status,
                    t.Code, t.Content, t.ReferenceCode, t.MatchedPaymentDetailId,
                    t.TransactionDate, t.CreatedAt))
                .ToListAsync(ct);

            // Group SePay transactions into their correct payment accounts
            var txByPaymentId = transactions
                .GroupBy(t => t.MatchedPaymentDetailId!.Value)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<TransactionLine>)g.ToList());

            var paymentLines = payments
                .Select(p => new PaymentLine(
                    p.Id, p.PaymentMethodId, p.PaymentMethodCode, p.PaymentMethodName,
                    p.Amount, p.Status, p.TransactionRef, p.Notes, p.ParentPaymentDetailId,
                    p.ProcessedByStaffId, p.ProcessedAt, p.CreatedAt, p.UpdatedAt,
                    txByPaymentId.TryGetValue(p.Id, out var txs) ? txs : Array.Empty<TransactionLine>()))
                .ToList();

            var remaining = ticket.TotalAmount - ticket.PaidAmount;

            return Result.Success(new Response(
                ticket.Id,
                ticket.Code,
                ticket.Status,
                (long)ticket.TotalAmount,
                (long)ticket.PaidAmount,
                (long)(remaining < 0 ? 0 : remaining),
                (long)ticket.RefundAmount,
                remaining <= 0,
                paymentLines));
        }
    }
}
