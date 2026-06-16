using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Inventory;

namespace Rpom.Application.Inventory.ListStockMovements;

public static class ListStockMovements
{
    public sealed record Query(
        int? ItemId,
        string? MovementType,
        DateTime? From,
        DateTime? To,
        int PageNumber,
        int PageSize) : IQuery<Page<Response>>;

    public sealed record Response(
        long Id,
        int ItemId,
        string ItemCode,
        string ItemName,
        string MovementType,
        decimal QtyInBase,
        decimal BalanceAfter,
        string? ReferenceType,
        long? ReferenceId,
        string? Reason,
        int CreatedByStaffId,
        string CreatedByStaffName,
        DateTime CreatedAt);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, Page<Response>>
    {
        public async Task<Result<Page<Response>>> Handle(Query request, CancellationToken ct)
        {
            IQueryable<StockMovement> q = dbContext.StockMovements.AsQueryable();

            if (request.ItemId.HasValue)
                q = q.Where(sm => sm.ItemId == request.ItemId.Value);

            if (!string.IsNullOrWhiteSpace(request.MovementType))
                q = q.Where(sm => sm.MovementType == request.MovementType);

            if (request.From.HasValue)
                q = q.Where(sm => sm.CreatedAt >= request.From.Value);

            if (request.To.HasValue)
                q = q.Where(sm => sm.CreatedAt <= request.To.Value);

            int totalCount = await q.CountAsync(ct);

            var rows = await q
                .OrderByDescending(sm => sm.Id)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(sm => new Response(
                    sm.Id,
                    sm.ItemId,
                    sm.Item.Code,
                    sm.Item.Name,
                    sm.MovementType,
                    sm.QtyInBase,
                    sm.BalanceAfter,
                    sm.ReferenceType,
                    sm.ReferenceId,
                    sm.Reason,
                    sm.CreatedByStaffId,
                    sm.CreatedByStaff.FullName,
                    sm.CreatedAt))
                .ToListAsync(ct);

            return Result.Success(new Page<Response>(rows, totalCount, request.PageNumber, request.PageSize));
        }
    }
}
