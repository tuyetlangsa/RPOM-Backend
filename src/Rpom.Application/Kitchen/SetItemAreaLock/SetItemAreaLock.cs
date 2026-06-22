using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;
using Rpom.Domain.Operations;

namespace Rpom.Application.Kitchen.SetItemAreaLock;

/// <summary>
///     Bếp khoá/mở "hết hàng" một món **theo khu vực** (F5). Lock → INSERT dòng
///     <see cref="ItemAreaLock"/>, unlock → DELETE — trạng thái hiện tại, lịch sử nằm ở AuditLog.
///     Grain theo Area vì món bán theo area; bếp có thể khoá ở area này để ưu tiên area khác.
///     Gate theo khu bếp của phiên (<c>KitchenStationId</c>) + area phải thực sự phục vụ món.
///     Phát <see cref="StaffNotification"/> tới counter của các area bị tác động. Quyền
///     <c>item:toggle_availability</c>.
/// </summary>
public static class SetItemAreaLock
{
    /// <summary>
    ///     Lock=true để đánh hết hàng, false để mở lại. Chọn area theo <paramref name="AreaIds"/>,
    ///     hoặc đặt <paramref name="AllServingAreas"/>=true để áp cho mọi area đang phục vụ món.
    /// </summary>
    public sealed record Command(
        int ItemId,
        bool Lock,
        IReadOnlyList<int>? AreaIds,
        bool AllServingAreas,
        string? Note) : ICommand<Response>;

    public sealed record Response(
        int ItemId,
        bool Locked,
        IReadOnlyList<int> AffectedAreaIds,
        IReadOnlyList<int> NotifiedCounterIds);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.ItemId).GreaterThan(0);
            RuleFor(x => x.Note).MaximumLength(200);
            RuleFor(x => x.AreaIds)
                .Must(ids => ids is { Count: > 0 })
                .When(x => !x.AllServingAreas)
                .WithMessage("Phải chọn ít nhất 1 khu vực, hoặc đặt allServingAreas = true.");
        }
    }

    internal sealed class Handler(
        IDbContext db,
        ICurrentStaff currentStaff,
        IDateTimeProvider clock,
        IVersionService versionService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            int? stationId = currentStaff.KitchenStationId;
            if (stationId is null) return Result.Failure<Response>(KitchenStationErrors.NotSelected);

            Item? item = await db.Items.FirstOrDefaultAsync(x => x.Id == request.ItemId, ct);
            if (item is null) return Result.Failure<Response>(ItemErrors.NotFound);
            if (item.KitchenStationId is null) return Result.Failure<Response>(ItemErrors.NotKitchenRouted);
            if (item.KitchenStationId != stationId.Value) return Result.Failure<Response>(ItemErrors.WrongStation);

            // Areas phục vụ món: category của món + tổ tiên (Category.Path) → AreaMenuCategory → Area.
            var itemCatIds = await db.ItemCategories
                .Where(ic => ic.ItemId == item.Id).Select(ic => ic.CategoryId).ToListAsync(ct);
            var paths = await db.Categories
                .Where(c => itemCatIds.Contains(c.Id)).Select(c => c.Path).ToListAsync(ct);
            var menuCatIds = paths
                .SelectMany(p => p.Split(';', StringSplitOptions.RemoveEmptyEntries))
                .Select(int.Parse).Distinct().ToList();
            var servingAreaIds = await db.AreaMenuCategories
                .Where(amc => menuCatIds.Contains(amc.CategoryId))
                .Select(amc => amc.AreaId).Distinct().ToListAsync(ct);
            var servingSet = servingAreaIds.ToHashSet();

            List<int> targetAreaIds;
            if (request.AllServingAreas)
            {
                targetAreaIds = servingAreaIds;
            }
            else
            {
                targetAreaIds = request.AreaIds!.Distinct().ToList();
                if (targetAreaIds.Any(a => !servingSet.Contains(a)))
                    return Result.Failure<Response>(ItemErrors.AreaNotServing);
            }

            DateTime now = clock.UtcNow;
            int staffId = currentStaff.StaffAccountId;

            var existing = await db.ItemAreaLocks
                .Where(l => l.ItemId == item.Id && targetAreaIds.Contains(l.AreaId))
                .ToListAsync(ct);
            var existingAreaIds = existing.Select(l => l.AreaId).ToHashSet();

            // Affected = chỉ những area thực sự đổi trạng thái → tránh spam thông báo.
            List<int> affectedAreaIds;
            if (request.Lock)
            {
                affectedAreaIds = targetAreaIds.Where(a => !existingAreaIds.Contains(a)).ToList();
                foreach (int areaId in affectedAreaIds)
                {
                    db.ItemAreaLocks.Add(new ItemAreaLock
                    {
                        ItemId = item.Id,
                        AreaId = areaId,
                        LockedByStaffId = staffId,
                        Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                        LockedAt = now,
                    });
                }
            }
            else
            {
                affectedAreaIds = existing.Select(l => l.AreaId).ToList();
                db.ItemAreaLocks.RemoveRange(existing);
            }

            // Không đổi gì → no-op, không ghi audit/notification/version.
            if (affectedAreaIds.Count == 0)
                return Result.Success(new Response(item.Id, request.Lock, [], []));

            // Mỗi area bị tác động → 1 thông báo riêng (route tới counter của area đó),
            // để FOH biết chính xác area thay vì chỉ "cả quầy".
            var affectedAreas = await db.Areas
                .Where(a => affectedAreaIds.Contains(a.Id))
                .Select(a => new { a.Id, a.Name, a.CounterId })
                .ToListAsync(ct);

            string title = request.Lock ? "Hết hàng" : "Còn hàng trở lại";
            string type = request.Lock
                ? StaffNotificationType.ItemOutOfStock
                : StaffNotificationType.ItemBackInStock;
            string noteSuffix = string.IsNullOrWhiteSpace(request.Note)
                ? "" : $" ({request.Note.Trim()})";

            foreach (var area in affectedAreas)
            {
                string body = (request.Lock
                    ? $"{item.Name} đã hết hàng tại {area.Name} — không nhận thêm vào đơn mới."
                    : $"{item.Name} đã có hàng trở lại tại {area.Name}.") + noteSuffix;

                db.StaffNotifications.Add(new StaffNotification
                {
                    CounterId = area.CounterId,
                    AreaId = area.Id,
                    Type = type,
                    Title = title,
                    Body = body,
                    RefItemId = item.Id,
                    CreatedByStaffId = staffId,
                    CreatedAt = now,
                });
            }

            var notifiedCounterIds = affectedAreas.Select(a => a.CounterId).Distinct().ToList();

            StaffAccount staff = await db.StaffAccounts.FirstAsync(s => s.Id == staffId, ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Item),
                EntityId = item.Id,
                Action = request.Lock ? "ITEM_LOCK" : "ITEM_UNLOCK",
                ActorStaffAccountId = staffId,
                ActorFullName = staff.FullName,
                Timestamp = now,
                Summary = $"{title}: {item.Code} | area [{string.Join(",", affectedAreaIds)}]"
                          + (string.IsNullOrWhiteSpace(request.Note) ? "" : $" | {request.Note.Trim()}"),
            });

            await db.SaveChangesAsync(ct);

            await versionService.BumpAsync(VersionScopes.Menu, $"ItemAreaLock(id={item.Id})", ct);
            await versionService.BumpAsync(VersionScopes.FloorPlan, $"ItemAreaLock(id={item.Id})", ct);

            return Result.Success(new Response(
                item.Id, request.Lock, affectedAreaIds, notifiedCounterIds));
        }
    }
}
