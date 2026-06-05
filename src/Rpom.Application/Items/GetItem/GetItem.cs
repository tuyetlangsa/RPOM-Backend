using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Application.Items.GetItem;

public static class GetItem
{
    public sealed record Query(int Id) : IQuery<Response>;

    public sealed record CategoryAssignment(int CategoryId, string Name, bool IsMain);

    public sealed record Response(
        int Id,
        string Code,
        string Name,
        string? Description,
        string? ImageUrl,
        int BaseUomId,
        string BaseUomCode,
        string BaseUomName,
        decimal VatPercent,
        bool IsStockable,
        bool HasRecipe,
        decimal? LowStockThreshold,
        int? KitchenStationId,
        string? KitchenStationName,
        bool IsActive,
        IReadOnlyList<CategoryAssignment> Categories,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            var row = await dbContext.Items
                .Where(x => x.Id == request.Id)
                .Select(x => new
                {
                    x.Id, x.Code, x.Name, x.Description, x.ImageUrl,
                    x.BaseUomId,
                    BaseUomCode = x.BaseUom.Code,
                    BaseUomName = x.BaseUom.Name,
                    x.VatPercent, x.IsStockable, x.HasRecipe, x.LowStockThreshold,
                    x.KitchenStationId,
                    KitchenStationName = x.KitchenStation != null ? x.KitchenStation.Name : null,
                    x.IsActive,
                    Categories = x.ItemCategories
                        .Select(ic => new CategoryAssignment(ic.CategoryId, ic.Category.Name, ic.IsMain))
                        .ToList(),
                    x.CreatedAt, x.UpdatedAt,
                })
                .FirstOrDefaultAsync(ct);

            if (row is null) return Result.Failure<Response>(ItemErrors.NotFound);

            return Result.Success(new Response(
                row.Id, row.Code, row.Name, row.Description, row.ImageUrl,
                row.BaseUomId, row.BaseUomCode, row.BaseUomName,
                row.VatPercent, row.IsStockable, row.HasRecipe, row.LowStockThreshold,
                row.KitchenStationId, row.KitchenStationName,
                row.IsActive, row.Categories, row.CreatedAt, row.UpdatedAt));
        }
    }
}
