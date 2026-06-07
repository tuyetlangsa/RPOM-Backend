namespace Rpom.Domain.Sales;
public static class PaymentTransactionStatus
{
    public const string Matched = "MATCHED";  //linked to a PENDING <see cref="TicketPaymentDetail"/> and settled it
    public const string Unmatched = "UNMATCHED";  //no matching pending payment found (kept for manual reconciliation)
    public const string Mismatch = "MISMATCH";  //payment found but transferred amount ≠ expected amount
    public const string Duplicate = "DUPLICATE";  //gateway re-delivered a transaction already processed (idempotent no-op)
    public const string Ignored = "IGNORED";  //irrelevant transaction (e.g. outbound transfer)
}
