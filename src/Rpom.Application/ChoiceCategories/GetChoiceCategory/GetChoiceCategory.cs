using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;

namespace Rpom.Application.ChoiceCategories.GetChoiceCategory;

public static class GetChoiceCategory
{
    public sealed record Query(int Id) : IQuery<Response>;

    public sealed record Response(
        int Id,
        string Name,
        string? Note,
        short MinChoice,
        short? MaxChoice,
        short DisplayOrder,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        IReadOnlyList<ModifierRef> Modifiers);

    public sealed record ModifierRef(
        int ItemId,
        string ItemCode,
        string ItemName,
        decimal ExtraPrice,
        int MinPerModifier,
        int MaxPerModifier,
        short DisplayOrder,
        bool IsActive);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            var cc = await db.ChoiceCategories
                .Where(c => c.Id == request.Id)
                .Select(c => new
                {
                    c.Id, c.Name, c.Note, c.MinChoice, c.MaxChoice, c.DisplayOrder, c.IsActive,
                    c.CreatedAt, c.UpdatedAt
                })
                .FirstOrDefaultAsync(ct);
            if (cc is null)
            {
                return Result.Failure<Response>(ChoiceCategoryErrors.NotFound);
            }

            List<ModifierRef> modifiers = await db.Modifiers
                .Where(m => m.ChoiceCategoryId == request.Id)
                .OrderBy(m => m.DisplayOrder)
                .Select(m => new ModifierRef(
                    m.ItemId, m.Item.Code, m.Item.Name, m.ExtraPrice,
                    m.MinPerModifier, m.MaxPerModifier, m.DisplayOrder, m.IsActive))
                .ToListAsync(ct);

            return Result.Success(new Response(
                cc.Id, cc.Name, cc.Note, cc.MinChoice, cc.MaxChoice, cc.DisplayOrder, cc.IsActive,
                cc.CreatedAt, cc.UpdatedAt, modifiers));
        }
    }
}
