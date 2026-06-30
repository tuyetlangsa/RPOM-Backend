using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;
using Rpom.Domain.Sales;

namespace Rpom.Application.Kitchen.GetIngredients;

/// <summary>
///     Tồn kho của MỘT khu bếp (kitchen station) cho màn hình bếp/kho — gồm 2 loại stockable item:
///     <list type="bullet">
///         <item><b>MATERIAL</b>: nguyên vật liệu được tiêu thụ (qua BOM active) bởi bất kỳ món bán nào
///         thuộc station này.</item>
///         <item><b>MENU_ITEM</b>: chính món bán <c>IsStockable=true</c> thuộc station (trừ trực tiếp bản thân,
///         không qua recipe).</item>
///     </list>
///     Mỗi dòng kèm tồn hiện tại (<c>CurrentQty</c>) + ngưỡng cảnh báo (<c>LowStockThreshold</c>).
///     <para>
///     Mở rộng: truyền <c>orderItemId</c> (thuộc station này) → tách riêng các stockable item liên quan tới
///     đúng món đó (nguyên liệu theo BOM, hoặc chính nó nếu là stockable không recipe) vào
///     <see cref="Response.OrderItemStocks"/>; phần còn lại nằm ở <see cref="Response.OtherStocks"/> (không
///     trùng lặp). Không truyền → <c>OrderItemStocks</c> rỗng, tất cả nằm ở <c>OtherStocks</c>.
///     </para>
///     Permission <c>kds:view</c>.
/// </summary>
public static class GetIngredients
{
    public const string KindMaterial = "MATERIAL";
    public const string KindMenuItem = "MENU_ITEM";

    public sealed record Query(long? OrderItemId, string? Search, bool? IsActive)
        : IQuery<Response>;

    public sealed record Response(
        IReadOnlyList<StockItem> OrderItemStocks,
        IReadOnlyList<StockItem> OtherStocks);

    public sealed record StockItem(
        int Id,
        string Code,
        string Name,
        string BaseUomCode,
        string Kind,
        bool IsStockable,
        bool HasRecipe,
        bool IsActive,
        decimal CurrentQty,
        decimal? LowStockThreshold,
        int? PrimaryCategoryId,
        string? PrimaryCategoryName);

    internal sealed class Handler(IDbContext dbContext, ICurrentStaff currentStaff)
        : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            int? stationId = currentStaff.KitchenStationId;
            if (stationId is null)
                return Result.Failure<Response>(KitchenStationErrors.NotSelected);
            int station = stationId.Value;

            bool stationOk = await dbContext.KitchenStations
                .AsNoTracking()
                .AnyAsync(s => s.Id == station && s.IsActive, ct);
            if (!stationOk)
                return Result.Failure<Response>(KitchenStationErrors.NotFound);

            // Nếu truyền orderItemId → xác định tập stockable item liên quan để tách riêng.
            HashSet<int> relatedIds = [];
            if (request.OrderItemId is long oiId)
            {
                var oi = await dbContext.OrderItems
                    .AsNoTracking()
                    .Where(o => o.Id == oiId)
                    .Select(o => new
                    {
                        o.ItemId,
                        o.KitchenStationId,
                        o.Item.HasRecipe,
                        o.Item.IsStockable
                    })
                    .FirstOrDefaultAsync(ct);

                if (oi is null)
                    return Result.Failure<Response>(OrderItemErrors.NotFound);
                if (oi.KitchenStationId != station)
                    return Result.Failure<Response>(OrderItemErrors.WrongStation);

                if (oi.HasRecipe)
                {
                    relatedIds = (await dbContext.BomLines
                        .AsNoTracking()
                        .Where(bl => bl.SellableItemId == oi.ItemId && bl.IsActive)
                        .Select(bl => bl.MaterialItemId)
                        .Distinct()
                        .ToListAsync(ct)).ToHashSet();
                }
                else if (oi.IsStockable)
                {
                    relatedIds = [oi.ItemId];
                }
            }

            // Tập stockable item của station: (A) nguyên liệu tiêu thụ qua BOM bởi món của station,
            // (B) chính món bán stockable thuộc station.
            var q = dbContext.Items
                .AsNoTracking()
                .Where(x => x.IsStockable
                            && (x.KitchenStationId == station
                                || dbContext.BomLines.Any(bl =>
                                    bl.MaterialItemId == x.Id
                                    && bl.IsActive
                                    && bl.SellableItem.KitchenStationId == station)));

            if (request.IsActive.HasValue)
                q = q.Where(x => x.IsActive == request.IsActive.Value);

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var s = request.Search.Trim().ToLower();
                q = q.Where(x => x.Code.ToLower().Contains(s) || x.Name.ToLower().Contains(s));
            }

            var rows = await q
                .OrderBy(x => x.Name)
                .Select(x => new StockItem(
                    x.Id,
                    x.Code,
                    x.Name,
                    x.BaseUom.Code,
                    x.KitchenStationId == station ? KindMenuItem : KindMaterial,
                    x.IsStockable,
                    x.HasRecipe,
                    x.IsActive,
                    dbContext.ItemStocks
                        .Where(st => st.ItemId == x.Id)
                        .Select(st => (decimal?)st.CurrentQty)
                        .FirstOrDefault() ?? 0m,
                    x.LowStockThreshold,
                    x.ItemCategories.Where(ic => ic.IsMain).Select(ic => (int?)ic.CategoryId).FirstOrDefault(),
                    x.ItemCategories.Where(ic => ic.IsMain).Select(ic => ic.Category.Name).FirstOrDefault()))
                .ToListAsync(ct);

            var orderItemStocks = relatedIds.Count == 0
                ? []
                : rows.Where(r => relatedIds.Contains(r.Id)).ToList();
            var otherStocks = relatedIds.Count == 0
                ? rows
                : rows.Where(r => !relatedIds.Contains(r.Id)).ToList();

            return Result.Success(new Response(orderItemStocks, otherStocks));
        }
    }
}
