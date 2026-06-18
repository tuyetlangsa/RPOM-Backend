using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;
using Rpom.Domain.Sales;

namespace Rpom.Application.Kitchen.GetKitchenOrders;
public static class GetKitchenOrders
{
    public sealed record Query() : IQuery<Response>;

    public sealed record Response(
        int KitchenStationId,
        string KitchenStationName,
        IReadOnlyList<OrderLiist> Orders);

    public sealed record OrderLiist(
        long OrderId,
        short OrderNumber,
        string OrderStatus,
        DateTime? SentAt,
        long TicketId,
        string TicketCode,
        int TableId,
        string TableCode,
        string AreaName,
        IReadOnlyList<ItemList> Items);

    public sealed record ItemList(
        long OrderItemId,
        int ItemId,
        string ItemCode,
        string ItemName,
        decimal Quantity,
        string UomCode,
        string Status,
        DateTime SentAt,
        DateTime? StartCookAt,
        DateTime? ReadyAt,
        string? Notes,
        IReadOnlyList<ModifierList> Modifiers);

    public sealed record ModifierList(string ItemName, decimal Quantity, string ComponentType);

    internal sealed class Handler(IDbContext dbContext, ICurrentStaff currentStaff)
        : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            int? stationId = currentStaff.KitchenStationId;
            if (stationId is null)
                return Result.Failure<Response>(KitchenStationErrors.NotSelected);

            var station = await dbContext.KitchenStations
                .AsNoTracking()
                .Where(s => s.Id == stationId.Value && s.IsActive)
                .Select(s => new { s.Id, s.Name })
                .FirstOrDefaultAsync(ct);
            if (station is null)
                return Result.Failure<Response>(KitchenStationErrors.NotFound);

            var rows = await dbContext.OrderItems
                .AsNoTracking()
                .Where(oi => oi.KitchenStationId == stationId.Value
                             && (oi.Status == OrderItemStatus.Pending
                                 || oi.Status == OrderItemStatus.Processing
                                 || oi.Status == OrderItemStatus.Ready)
                             && (oi.Order.Status == OrderStatus.Sent
                                 || oi.Order.Status == OrderStatus.Processing)
                             && oi.Ticket.Status == TicketStatus.Open)
                .Select(oi => new
                {
                    oi.OrderId,
                    OrderNo = oi.Order.OrderNumber,
                    OrderStatus = oi.Order.Status,
                    OrderSentAt = oi.Order.SentAt,
                    oi.TicketId,
                    TicketCode = oi.Ticket.Code,
                    oi.Ticket.TableId,
                    TableCode = oi.Ticket.Table.Code,
                    AreaName = oi.Ticket.Area.Name,
                    Item = new
                    {
                        oi.Id, oi.ItemId, oi.ItemCode, oi.ItemName, oi.Quantity, oi.UomCode,
                        oi.Status, oi.SentAt, oi.StartCookAt, oi.ReadyAt, oi.Notes
                    },
                    Modifiers = oi.Details
                        .Select(d => new { d.ItemName, d.Quantity, d.ComponentType })
                        .ToList()
                })
                .ToListAsync(ct);

            var orders = rows
                .GroupBy(r => new
                {
                    r.OrderId, r.OrderNo, r.OrderStatus, r.OrderSentAt,
                    r.TicketId, r.TicketCode, r.TableId, r.TableCode, r.AreaName
                })
                .OrderBy(g => g.Key.OrderSentAt)
                .ThenBy(g => g.Key.OrderId)
                .Select(g => new OrderLiist(
                    g.Key.OrderId,
                    g.Key.OrderNo,
                    g.Key.OrderStatus,
                    g.Key.OrderSentAt,
                    g.Key.TicketId,
                    g.Key.TicketCode,
                    g.Key.TableId,
                    g.Key.TableCode,
                    g.Key.AreaName,
                    g.OrderBy(x => x.Item.SentAt).ThenBy(x => x.Item.Id)
                        .Select(x => new ItemList(
                            x.Item.Id, x.Item.ItemId, x.Item.ItemCode, x.Item.ItemName,
                            x.Item.Quantity, x.Item.UomCode, x.Item.Status,
                            x.Item.SentAt, x.Item.StartCookAt, x.Item.ReadyAt, x.Item.Notes,
                            x.Modifiers.Select(m => new ModifierList(m.ItemName, m.Quantity, m.ComponentType)).ToList()))
                        .ToList()))
                .ToList();

            return Result.Success(new Response(station.Id, station.Name, orders));
        }
    }
}
