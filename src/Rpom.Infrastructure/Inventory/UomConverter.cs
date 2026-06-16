using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Inventory;

namespace Rpom.Infrastructure.Inventory;

internal sealed class UomConverter(IDbContext dbContext) : IUomConverter
{
    public async Task<decimal?> ToBaseAsync(int itemId, int baseUomId, int uomId, decimal qty, CancellationToken ct)
    {
        decimal? factor = await FactorAsync(itemId, baseUomId, uomId, ct);
        return factor is null ? null : qty * factor.Value;
    }

    public async Task<bool> IsValidUomAsync(int itemId, int baseUomId, int uomId, CancellationToken ct)
    {
        return await FactorAsync(itemId, baseUomId, uomId, ct) is not null;
    }

    private async Task<decimal?> FactorAsync(int itemId, int baseUomId, int uomId, CancellationToken ct)
    {
        if (uomId == baseUomId)
        {
            return 1m;
        }

        return await dbContext.ItemUomConversions
            .Where(c => c.ItemId == itemId && c.UomId == uomId && c.IsActive)
            .Select(c => (decimal?)c.FactorToBase)
            .FirstOrDefaultAsync(ct);
    }
}
