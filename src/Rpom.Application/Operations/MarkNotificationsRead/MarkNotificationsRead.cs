using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;

namespace Rpom.Application.Operations.MarkNotificationsRead;

/// <summary>
///     Nhân viên hiện tại đánh dấu đã xem thông báo của một quầy (bấm vào icon chuông) → đẩy
///     read-cursor lên. <c>UpToId</c> null → đánh dấu tới notification mới nhất của quầy. Cursor
///     chỉ tiến, không lùi. Sau bước này badge chưa đọc về 0 (hoặc giảm tới UpToId).
///     Quyền <c>notification:view</c>.
/// </summary>
public static class MarkNotificationsRead
{
    public sealed record Command(int CounterId, long? UpToId) : ICommand<Response>;

    public sealed record Response(int CounterId, long LastReadId);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(x => x.CounterId).GreaterThan(0);
    }

    internal sealed class Handler(IDbContext db, ICurrentStaff currentStaff, IDateTimeProvider clock)
        : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            int staffId = currentStaff.StaffAccountId;

            long target = request.UpToId ?? await db.StaffNotifications
                .Where(n => n.CounterId == request.CounterId)
                .OrderByDescending(n => n.Id)
                .Select(n => (long?)n.Id)
                .FirstOrDefaultAsync(ct) ?? 0L;

            NotificationReadState? state = await db.NotificationReadStates
                .FirstOrDefaultAsync(r => r.StaffAccountId == staffId && r.CounterId == request.CounterId, ct);

            DateTime now = clock.UtcNow;
            if (state is null)
            {
                state = new NotificationReadState
                {
                    StaffAccountId = staffId,
                    CounterId = request.CounterId,
                    LastReadNotificationId = target,
                    UpdatedAt = now,
                };
                db.NotificationReadStates.Add(state);
            }
            else if (target > state.LastReadNotificationId) // cursor chỉ tiến
            {
                state.LastReadNotificationId = target;
                state.UpdatedAt = now;
            }

            await db.SaveChangesAsync(ct);
            return Result.Success(new Response(request.CounterId, state.LastReadNotificationId));
        }
    }
}
