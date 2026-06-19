using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Common;

namespace Rpom.Application.Operations.GetUnreadNotificationCount;

/// <summary>
///     Badge số thông báo CHƯA XEM của nhân viên hiện tại tại một quầy: đếm
///     <see cref="Domain.Operations.StaffNotification"/> của quầy có <c>Id &gt;</c> read-cursor
///     của nhân viên. FE poll cùng nhịp với GetNotifications để hiển thị số trên icon chuông.
///     Quyền <c>notification:view</c>.
/// </summary>
public static class GetUnreadNotificationCount
{
    public sealed record Query(int CounterId) : IQuery<Response>;

    public sealed record Response(int CounterId, int UnreadCount, long? LatestId, long LastReadId);

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator() => RuleFor(x => x.CounterId).GreaterThan(0);
    }

    internal sealed class Handler(IDbContext db, ICurrentStaff currentStaff)
        : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            int staffId = currentStaff.StaffAccountId;

            long lastReadId = await db.NotificationReadStates
                .Where(r => r.StaffAccountId == staffId && r.CounterId == request.CounterId)
                .Select(r => r.LastReadNotificationId)
                .FirstOrDefaultAsync(ct); // 0 nếu chưa có cursor

            long? latestId = await db.StaffNotifications
                .Where(n => n.CounterId == request.CounterId)
                .OrderByDescending(n => n.Id)
                .Select(n => (long?)n.Id)
                .FirstOrDefaultAsync(ct);

            int unread = await db.StaffNotifications
                .CountAsync(n => n.CounterId == request.CounterId && n.Id > lastReadId, ct);

            return Result.Success(new Response(request.CounterId, unread, latestId, lastReadId));
        }
    }
}
