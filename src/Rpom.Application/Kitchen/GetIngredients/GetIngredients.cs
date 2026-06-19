using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;

namespace Rpom.Application.Kitchen.GetIngredients;

/// <summary>
///     Nguyên vật liệu của một khu bếp (kitchen station) cho màn hình bếp/kho.
///     <para>
///     Ingredient = Item thuộc nhánh category gốc <c>NGUYEN_VAT_LIEU</c> (gốc + con).
///     Lọc theo station: chỉ lấy nguyên vật liệu được tiêu thụ bởi các món thuộc
///     station đó — tức có <see cref="Rpom.Domain.Inventory.BomLine"/> active nối tới
///     một món bán (SellableItem) có <c>KitchenStationId</c> = station đang xem.
///     Kèm tồn kho hiện tại (ItemStock.CurrentQty).
///     </para>
/// </summary>
public static class GetIngredients
{
    private const string MaterialRootCode = "NGUYEN_VAT_LIEU";

    public sealed record Query(string? Search, bool? IsActive)
        : IQuery<IReadOnlyList<Ingredient>>;

    public sealed record Ingredient(
        int Id,
        string Code,
        string Name,
        string BaseUomCode,
        bool IsStockable,
        bool HasRecipe,
        bool IsActive,
        decimal CurrentQty,
        int? PrimaryCategoryId,
        string? PrimaryCategoryName);

    internal sealed class Handler(IDbContext dbContext, ICurrentStaff currentStaff)
        : IQueryHandler<Query, IReadOnlyList<Ingredient>>
    {
        public async Task<Result<IReadOnlyList<Ingredient>>> Handle(Query request, CancellationToken ct)
        {
            int? stationId = currentStaff.KitchenStationId;
            if (stationId is null)
                return Result.Failure<IReadOnlyList<Ingredient>>(KitchenStationErrors.NotSelected);

            // Station must exist and be active
            bool stationOk = await dbContext.KitchenStations
                .AsNoTracking()
                .AnyAsync(s => s.Id == stationId.Value && s.IsActive, ct);
            if (!stationOk)
                return Result.Failure<IReadOnlyList<Ingredient>>(KitchenStationErrors.NotFound);

            // Category gốc nguyên vật liệu.
            var root = await dbContext.Categories
                .AsNoTracking()
                .Where(c => c.Code == MaterialRootCode)
                .Select(c => new { c.Id, c.Path })
                .FirstOrDefaultAsync(ct);
            if (root is null)
                return Result.Success<IReadOnlyList<Ingredient>>([]);

            // Gốc + toàn bộ category con.
            var subtreeIds = await dbContext.Categories
                .AsNoTracking()
                .Where(c => c.Id == root.Id || EF.Functions.Like(c.Path, root.Path + "%"))
                .Select(c => c.Id)
                .ToListAsync(ct);

            // Nguyên vật liệu thuộc nhánh category VÀ được dùng bởi món của station này (qua BOM).
            var q = dbContext.Items
                .AsNoTracking()
                .Where(x => x.ItemCategories.Any(ic => subtreeIds.Contains(ic.CategoryId))
                            && dbContext.BomLines.Any(bl =>
                                bl.MaterialItemId == x.Id
                                && bl.IsActive
                                && bl.SellableItem.KitchenStationId == stationId.Value));

            if (request.IsActive.HasValue)
                q = q.Where(x => x.IsActive == request.IsActive.Value);

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var s = request.Search.Trim().ToLower();
                q = q.Where(x => x.Code.ToLower().Contains(s) || x.Name.ToLower().Contains(s));
            }

            var rows = await q
                .OrderBy(x => x.Name)
                .Select(x => new Ingredient(
                    x.Id,
                    x.Code,
                    x.Name,
                    x.BaseUom.Code,
                    x.IsStockable,
                    x.HasRecipe,
                    x.IsActive,
                    dbContext.ItemStocks
                        .Where(st => st.ItemId == x.Id)
                        .Select(st => (decimal?)st.CurrentQty)
                        .FirstOrDefault() ?? 0m,
                    x.ItemCategories.Where(ic => ic.IsMain).Select(ic => (int?)ic.CategoryId).FirstOrDefault(),
                    x.ItemCategories.Where(ic => ic.IsMain).Select(ic => ic.Category.Name).FirstOrDefault()))
                .ToListAsync(ct);

            return Result.Success<IReadOnlyList<Ingredient>>(rows);
        }
    }
}
