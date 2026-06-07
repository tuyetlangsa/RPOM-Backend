using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Application.SetMenus.GetSetMenu;

public static class GetSetMenu
{
    public sealed record Query(int ItemId) : IQuery<Response>;

    public sealed record Response(
        int ItemId,
        string ItemCode,
        string ItemName,
        string? Description,
        IReadOnlyList<DetailRef> Details);

    public sealed record DetailRef(
        int Id,
        string DetailType,
        int? ComponentItemId,
        string? ComponentItemName,
        decimal? Quantity,
        bool? IsFixed,
        int? ChoiceCategoryId,
        string? ChoiceCategoryName,
        short DisplayOrder);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            var setMenu = await db.SetMenus
                .Where(s => s.ItemId == request.ItemId)
                .Select(s => new { s.ItemId, ItemCode = s.Item.Code, ItemName = s.Item.Name, s.Description })
                .FirstOrDefaultAsync(ct);
            if (setMenu is null)
            {
                return Result.Failure<Response>(SetMenuErrors.NotASetMenu);
            }

            List<DetailRef> details = await db.SetMenuDetails
                .Where(d => d.SetMenuItemId == request.ItemId)
                .OrderBy(d => d.DisplayOrder)
                .Select(d => new DetailRef(
                    d.Id,
                    d.DetailType,
                    d.ComponentItemId,
                    d.ComponentItem != null ? d.ComponentItem.Name : null,
                    d.Quantity,
                    d.IsFixed,
                    d.ChoiceCategoryId,
                    d.ChoiceCategory != null ? d.ChoiceCategory.Name : null,
                    d.DisplayOrder))
                .ToListAsync(ct);

            return Result.Success(new Response(
                setMenu.ItemId, setMenu.ItemCode, setMenu.ItemName, setMenu.Description, details));
        }
    }
}
