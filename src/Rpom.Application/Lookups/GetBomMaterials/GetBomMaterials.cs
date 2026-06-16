using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Lookups.GetBomMaterials;

/// <summary>
///     Items valid as a BOM material (recipe ingredient): IsStockable=true AND HasRecipe=false.
///     Mirrors CreateBomLine's guards (MaterialMustBeStockable + MaterialAlreadyHasRecipe) so the
///     BOM material picker never offers an invalid choice. Each row carries the base UoM for the
///     BOM quantity unit selector.
/// </summary>
public static class GetBomMaterials
{
    public sealed record Query(string? Search) : IQuery<IReadOnlyList<MaterialItem>>;

    public sealed record MaterialItem(
        int ItemId,
        string Code,
        string Name,
        int BaseUomId,
        string BaseUomCode,
        string BaseUomName);

    internal sealed class Handler(IDbContext dbContext)
        : IQueryHandler<Query, IReadOnlyList<MaterialItem>>
    {
        public async Task<Result<IReadOnlyList<MaterialItem>>> Handle(Query request, CancellationToken ct)
        {
            var q = dbContext.Items.Where(i => i.IsStockable && !i.HasRecipe && i.IsActive);

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                string s = request.Search.Trim().ToLower();
                q = q.Where(i => i.Code.ToLower().Contains(s) || i.Name.ToLower().Contains(s));
            }

            List<MaterialItem> rows = await q
                .OrderBy(i => i.Name)
                .Select(i => new MaterialItem(
                    i.Id, i.Code, i.Name, i.BaseUomId, i.BaseUom.Code, i.BaseUom.Name))
                .ToListAsync(ct);

            return Result.Success<IReadOnlyList<MaterialItem>>(rows);
        }
    }
}
