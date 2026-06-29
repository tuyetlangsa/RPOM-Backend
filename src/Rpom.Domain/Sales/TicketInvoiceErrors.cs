using Rpom.Domain.Common;

namespace Rpom.Domain.Sales;

public static class TicketInvoiceErrors
{
    public static readonly Error AlreadyExists = Error.Conflict(
        "TicketInvoice.AlreadyExists",
        "Ticket already has an invoice snapshot — each ticket can only be closed once.");
}
