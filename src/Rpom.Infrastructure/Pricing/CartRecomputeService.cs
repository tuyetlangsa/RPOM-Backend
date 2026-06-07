using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Domain.Sales;

namespace Rpom.Infrastructure.Pricing;

internal sealed class CartRecomputeService(
    IDbContext dbContext,
    IRoundingConfig rc,
    IDateTimeProvider clock)
    : ICartRecomputeService
{
    public async Task RecomputeAsync(long orderId, CancellationToken ct)
    {
        List<CartItem> cartItems = await dbContext.CartItems
            .Where(c => c.OrderId == orderId)
            .ToListAsync(ct);

        DateTime now = clock.UtcNow;
        foreach (CartItem c in cartItems)
        {
            // Cart is DRAFT — no discount applied (pricing spec §3.5).
            var input = new LinePricingInput(
                c.Quantity, c.UnitPrice, c.ChoicePricePerUnit,
                c.VatPercent, c.ServiceChargePercent, c.ServiceChargeVatPercent,
                0m, 0m);

            LinePricingResult r = PricingCalculator.ComputeLine(input, rc);

            c.LineSubtotal = r.LineSubtotal;
            c.ServiceChargeAmount = r.ServiceChargeAmount;
            c.VatItemAmount = r.VatItemAmount;
            c.VatScAmount = r.VatScAmount;
            c.VatAmount = r.VatAmount;
            c.LineTotal = r.LineTotal;
            c.UpdatedAt = now;
        }
    }
}
