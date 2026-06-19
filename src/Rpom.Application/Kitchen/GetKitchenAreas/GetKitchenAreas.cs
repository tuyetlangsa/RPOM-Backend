using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;

namespace Rpom.Application.Kitchen.GetKitchenAreas;

/// <summary>
///     Danh sách khu vực để BẾP chọn rồi xem menu + khoá/mở hết hàng. Chỉ trả area mà khu bếp
///     của phiên (<c>KitchenStationId</c> = station claim) thực sự **có món phục vụ** — vào area
///     không có món của bếp thì toggle vô nghĩa. Mỗi area kèm số món đang bị khoá tại đó để bếp
///     thấy nhanh. Quyền <c>kds:view</c>.
/// </summary>
public static class GetKitchenAreas
{
    public sealed record Query : IQuery<Response>;

    public sealed record Response(int KitchenStationId, IReadOnlyList<AreaInfo> Areas);

    public sealed record AreaInfo(
        int AreaId,
        string AreaName,
        int CounterId,
        string CounterName,
        short DisplayOrder,
        int LockedItemCount);

    internal sealed class Handler(IDbContext db, ICurrentStaff currentStaff)
        : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            int? stationId = currentStaff.KitchenStationId;
            if (stationId is null) return Result.Failure<Response>(KitchenStationErrors.NotSelected);

            // Món thuộc khu bếp của phiên → category của chúng + tổ tiên → AreaMenuCategory → area.
            var stationItemIds = await db.Items
                .Where(i => i.IsActive && i.KitchenStationId == stationId.Value)
                .Select(i => i.Id).ToListAsync(ct);
            if (stationItemIds.Count == 0)
                return Result.Success(new Response(stationId.Value, []));

            var catIds = await db.ItemCategories
                .Where(ic => stationItemIds.Contains(ic.ItemId))
                .Select(ic => ic.CategoryId).Distinct().ToListAsync(ct);
            var paths = await db.Categories
                .Where(c => catIds.Contains(c.Id)).Select(c => c.Path).ToListAsync(ct);
            var menuCatIds = paths
                .SelectMany(p => p.Split(';', StringSplitOptions.RemoveEmptyEntries))
                .Select(int.Parse).Distinct().ToList();

            var areaIds = await db.AreaMenuCategories
                .Where(amc => menuCatIds.Contains(amc.CategoryId))
                .Select(amc => amc.AreaId).Distinct().ToListAsync(ct);

            // Số món của bếp đang bị khoá theo từng area.
            var lockedCountByArea = (await db.ItemAreaLocks
                    .Where(l => areaIds.Contains(l.AreaId) && stationItemIds.Contains(l.ItemId))
                    .GroupBy(l => l.AreaId)
                    .Select(g => new { AreaId = g.Key, Count = g.Count() })
                    .ToListAsync(ct))
                .ToDictionary(x => x.AreaId, x => x.Count);

            var areas = await db.Areas
                .Where(a => areaIds.Contains(a.Id) && a.IsActive)
                .OrderBy(a => a.CounterId).ThenBy(a => a.DisplayOrder)
                .Select(a => new
                {
                    a.Id,
                    a.Name,
                    a.CounterId,
                    CounterName = a.Counter.Name,
                    a.DisplayOrder
                })
                .ToListAsync(ct);

            var dtos = areas.Select(a => new AreaInfo(
                a.Id, a.Name, a.CounterId, a.CounterName, a.DisplayOrder,
                lockedCountByArea.GetValueOrDefault(a.Id, 0))).ToList();

            return Result.Success(new Response(stationId.Value, dtos));
        }
    }
}
