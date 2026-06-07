using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
using Rpom.Domain.Menu;
using Rpom.Domain.Restaurant;

namespace Rpom.Application.Areas.SetAreaMenuCategories;

/// <summary>
/// Full-snapshot replace: FE gửi nguyên tập CategoryId mong muốn cho 1 Area;
/// BE diff với bảng nối AreaMenuCategory — thêm id mới, xoá id bị bỏ. Lưu đúng
/// id admin chọn (cả cha lẫn con); bung subtree là việc của GetMenu. Empty hợp lệ
/// (gỡ hết → menu area rỗng). Bump scope MENU để cashier GetMenu refetch.
/// </summary>
public static class SetAreaMenuCategories
{
    public sealed record Command(
        int AreaId,
        IReadOnlyList<int> CategoryIds) : ICommand<Response>;

    public sealed record Response(int AreaId, int Inserted, int Deleted, int Total);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.AreaId).GreaterThan(0);
            RuleFor(x => x.CategoryIds).NotNull();
            RuleForEach(x => x.CategoryIds).GreaterThan(0);
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
            var areaExists = await db.Areas.AnyAsync(a => a.Id == request.AreaId, ct);
            if (!areaExists) return Result.Failure<Response>(AreaErrors.NotFound);

            var wantedIds = request.CategoryIds.Distinct().ToList();

            // Validate category tồn tại & active (chỉ cho gán category đang dùng được).
            if (wantedIds.Count > 0)
            {
                var validCount = await db.Categories
                    .CountAsync(c => wantedIds.Contains(c.Id) && c.IsActive, ct);
                if (validCount != wantedIds.Count)
                    return Result.Failure<Response>(CategoryErrors.NotFound);
            }

            var existing = await db.AreaMenuCategories
                .Where(amc => amc.AreaId == request.AreaId)
                .ToListAsync(ct);
            var existingIds = existing.Select(e => e.CategoryId).ToHashSet();
            var wantedSet = wantedIds.ToHashSet();

            var now = clock.UtcNow;
            var inserted = 0;
            var deleted = 0;

            // Insert id mới
            foreach (var id in wantedIds)
            {
                if (!existingIds.Contains(id))
                {
                    db.AreaMenuCategories.Add(new AreaMenuCategory
                    {
                        AreaId = request.AreaId,
                        CategoryId = id,
                        CreatedAt = now,
                    });
                    inserted++;
                }
            }

            // Delete id không còn trong payload
            foreach (var row in existing)
            {
                if (!wantedSet.Contains(row.CategoryId))
                {
                    db.AreaMenuCategories.Remove(row);
                    deleted++;
                }
            }

            if (inserted > 0 || deleted > 0)
            {
                var staffId = currentStaff.StaffAccountId;
                var staff = await db.StaffAccounts.FirstAsync(x => x.Id == staffId, ct);
                db.AuditLogs.Add(new AuditLog
                {
                    EntityType = nameof(Area),
                    EntityId = request.AreaId,
                    Action = "SET_MENU_CATEGORIES",
                    ActorStaffAccountId = staffId,
                    ActorFullName = staff.FullName,
                    Timestamp = now,
                    Summary = $"AreaMenuCategory set: +{inserted} -{deleted} on area {request.AreaId}",
                });
                await db.SaveChangesAsync(ct);
                await versionService.BumpAsync(VersionScopes.Menu,
                    $"AreaMenuCategory.Set(areaId={request.AreaId})", ct);
            }

            return Result.Success(new Response(
                request.AreaId, inserted, deleted, wantedIds.Count));
        }
    }
}
