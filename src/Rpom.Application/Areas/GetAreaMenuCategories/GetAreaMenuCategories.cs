using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Application.Areas.GetAreaMenuCategories;

/// <summary>
///     Đọc danh sách Category đang được gán trực tiếp cho 1 Area (bảng nối
///     AreaMenuCategory). Chỉ trả category gán trực tiếp — việc bung subtree là
///     của read-side GetMenu (qua Category.Path prefix).
/// </summary>
public static class GetAreaMenuCategories
{
    public sealed record Query(int AreaId) : IQuery<Response>;

    public sealed record Response(int AreaId, IReadOnlyList<CategoryRef> Categories);

    public sealed record CategoryRef(
        int CategoryId,
        string Code,
        string Name,
        string Path,
        short Level);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            bool areaExists = await db.Areas.AnyAsync(a => a.Id == request.AreaId, ct);
            if (!areaExists)
            {
                return Result.Failure<Response>(AreaErrors.NotFound);
            }

            List<CategoryRef> categories = await db.AreaMenuCategories
                .Where(amc => amc.AreaId == request.AreaId)
                .OrderBy(amc => amc.Category.DisplayOrder)
                .Select(amc => new CategoryRef(
                    amc.CategoryId,
                    amc.Category.Code,
                    amc.Category.Name,
                    amc.Category.Path,
                    amc.Category.Level))
                .ToListAsync(ct);

            return Result.Success(new Response(request.AreaId, categories));
        }
    }
}
