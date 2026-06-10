using Rpom.Domain.Common;

namespace Rpom.Domain.Sales;
public class PaymentTransaction : Entity
{
    public long Id { get; set; }

    public string Gateway { get; set; } = "SEPAY";

    public long GatewayTransactionId { get; set; }

    public string? BankBrand { get; set; }

    public string? AccountNumber { get; set; }

    public string? SubAccount { get; set; }  //optional, can be empty ""

    public string TransferType { get; set; } = null!;  //"in" (incoming)

    public decimal TransferAmount { get; set; }

    public decimal Accumulated { get; set; }  //It could be 0, the bank didn't pay the balance => it's 0 (from webhook)

    public string? Code { get; set; }

    public string? Content { get; set; }

    public string? ReferenceCode { get; set; }

    public string? Description { get; set; }

    public DateTime TransactionDate { get; set; }

    public string? RawPayload { get; set; }

    public string Status { get; set; } = PaymentTransactionStatus.Unmatched;  //for the transaction status from the SePay webhook

    public string? MatchedReferenceCode { get; set; }

    public long? MatchedPaymentDetailId { get; set; }  //null while unmatched

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual TicketPaymentDetail? MatchedPaymentDetail { get; set; }
}
