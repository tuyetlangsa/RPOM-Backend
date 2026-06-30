using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;
using Rpom.Domain.Sales;

namespace Rpom.Application.Kitchen.GetKitchenOrders;

/// <summary>
///     KDS feed for the kitchen area of ​​the session, **grouped by Area** → Batch (Order) → Item. Each item is:
///     (a) a single OrderItem belonging to the station, or (b) a **set menu component** belonging to the station (each component
///     displayed as a dish, with its own Start Cook/Mark Ready operation, and the name of the parent set). Permission <c>kds:view</c>
/// </summary>
public static class GetKitchenOrders
{
    public sealed record Query : IQuery<Response>;

    public sealed record Response(
        int KitchenStationId,
        string KitchenStationName,
        IReadOnlyList<AreaGroup> Areas);

    public sealed record AreaGroup(int AreaId, string AreaName, IReadOnlyList<KitchenOrder> Orders);

    public sealed record KitchenOrder(
        long OrderId,
        short OrderNumber,
        string OrderStatus,
        DateTime? SentAt,
        long TicketId,
        string TicketCode,
        int TableId,
        string TableCode,
        IReadOnlyList<KitchenItem> Items);

    public sealed record KitchenItem(
        bool IsSetComponent,
        long Id,                  // OrderItemId (not set menu/combo) or OrderItemDetailId (component set)
        long? ParentOrderItemId,  // only component
        string? ParentSetName,    // only component
        int ItemId,
        string? ItemCode,
        string ItemName,
        decimal Quantity,
        string? UomCode,
        string Status,
        DateTime SentAt,
        DateTime? StartCookAt,
        DateTime? ReadyAt,
        string? Notes);

    private sealed record Flat(
        int AreaId, string AreaName,
        long OrderId, short OrderNo, string OrderStatus, DateTime? OrderSentAt,
        long TicketId, string TicketCode, int TableId, string TableCode,
        bool IsSetComponent, long Id, long? ParentOrderItemId, string? ParentSetName,
        int ItemId, string? ItemCode, string ItemName, decimal Quantity, string? UomCode,
        string Status, DateTime ItemSentAt, DateTime? StartCookAt, DateTime? ReadyAt, string? Notes);

    internal sealed class Handler(IDbContext db, ICurrentStaff currentStaff) : IQueryHandler<Query, Response>
    {
        private static readonly string[] ActiveStatuses =
            [OrderItemStatus.Pending, OrderItemStatus.Processing, OrderItemStatus.Ready];

        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            int? stationId = currentStaff.KitchenStationId;
            if (stationId is null) return Result.Failure<Response>(KitchenStationErrors.NotSelected);

            var station = await db.KitchenStations
                .Where(s => s.Id == stationId.Value && s.IsActive)
                .Select(s => new { s.Id, s.Name })
                .FirstOrDefaultAsync(ct);
            if (station is null) return Result.Failure<Response>(KitchenStationErrors.NotFound);

            // (a) Món đơn thuộc station (set container có station = null nên tự loại).
            var lineRows = await db.OrderItems
                .Where(oi => oi.KitchenStationId == stationId.Value
                             && ActiveStatuses.Contains(oi.Status)
                             && (oi.Order.Status == OrderStatus.Sent || oi.Order.Status == OrderStatus.Processing)
                             && oi.Ticket.Status == TicketStatus.Open)
                .Select(oi => new Flat(
                    oi.Ticket.AreaId, oi.Ticket.Area.Name,
                    oi.OrderId, oi.Order.OrderNumber, oi.Order.Status, oi.Order.SentAt,
                    oi.TicketId, oi.Ticket.Code, oi.Ticket.TableId, oi.Ticket.Table.Code,
                    false, oi.Id, null, null,
                    oi.ItemId, oi.ItemCode, oi.ItemName, oi.Quantity, oi.UomCode,
                    oi.Status, oi.SentAt, oi.StartCookAt, oi.ReadyAt, oi.Notes))
                .ToListAsync(ct);

            // (b) Thành phần set menu thuộc station.
            var compRows = await db.OrderItemDetails
                .Where(d => d.KitchenStationId == stationId.Value
                            && ActiveStatuses.Contains(d.Status)
                            && (d.OrderItem.Order.Status == OrderStatus.Sent
                                || d.OrderItem.Order.Status == OrderStatus.Processing)
                            && d.OrderItem.Ticket.Status == TicketStatus.Open)
                .Select(d => new Flat(
                    d.OrderItem.Ticket.AreaId, d.OrderItem.Ticket.Area.Name,
                    d.OrderItem.OrderId, d.OrderItem.Order.OrderNumber, d.OrderItem.Order.Status, d.OrderItem.Order.SentAt,
                    d.OrderItem.TicketId, d.OrderItem.Ticket.Code, d.OrderItem.Ticket.TableId, d.OrderItem.Ticket.Table.Code,
                    true, (long)d.Id, d.OrderItemId, d.OrderItem.ItemName,
                    d.ItemId, null, d.ItemName, d.Quantity * d.OrderItem.Quantity, null,
                    d.Status, d.OrderItem.SentAt, d.StartCookAt, d.ReadyAt, d.Notes))
                .ToListAsync(ct);

            var all = lineRows.Concat(compRows).ToList();

            var areas = all
                .GroupBy(r => new { r.AreaId, r.AreaName })
                .OrderBy(ga => ga.Key.AreaName)
                .Select(ga => new AreaGroup(
                    ga.Key.AreaId, ga.Key.AreaName,
                    ga.GroupBy(r => new
                    {
                        r.OrderId, r.OrderNo, r.OrderStatus, r.OrderSentAt,
                        r.TicketId, r.TicketCode, r.TableId, r.TableCode
                    })
                        .OrderBy(go => go.Key.OrderSentAt).ThenBy(go => go.Key.OrderId)
                        .Select(go => new KitchenOrder(
                            go.Key.OrderId, go.Key.OrderNo, go.Key.OrderStatus, go.Key.OrderSentAt,
                            go.Key.TicketId, go.Key.TicketCode, go.Key.TableId, go.Key.TableCode,
                            go.OrderBy(x => x.ItemSentAt).ThenBy(x => x.IsSetComponent).ThenBy(x => x.Id)
                                .Select(x => new KitchenItem(
                                    x.IsSetComponent, x.Id, x.ParentOrderItemId, x.ParentSetName,
                                    x.ItemId, x.ItemCode, x.ItemName, x.Quantity, x.UomCode,
                                    x.Status, x.ItemSentAt, x.StartCookAt, x.ReadyAt, x.Notes))
                                .ToList()))
                        .ToList()))
                .ToList();

            return Result.Success(new Response(station.Id, station.Name, areas));
        }
    }
}
