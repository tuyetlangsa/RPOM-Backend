using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Operations.GetNotifications;

/// <summary>
///     Poll broadcast operational notifications for a counter (Cashier / Order Staff
///     terminals). FE passes its current <c>counterId</c> + optional <c>sinceId</c> cursor;
///     returns notifications newer than the cursor, newest first. Quyền <c>notification:view</c>.
/// </summary>
public static class GetNotifications
{
    public sealed record Query(int CounterId, long? SinceId, int Limit = 50) : IQuery<Response>;

    public sealed record Response(IReadOnlyList<Notification> Notifications, long? LatestId);

    public sealed record Notification(
        long Id,
        string Type,
        string Title,
        string Body,
        int? RefItemId,
        int? AreaId,
        string? AreaName,
        DateTime CreatedAt);

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.CounterId).GreaterThan(0);
            RuleFor(x => x.Limit).InclusiveBetween(1, 200);
        }
    }

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            IQueryable<Domain.Operations.StaffNotification> q = db.StaffNotifications
                .Where(n => n.CounterId == request.CounterId);

            if (request.SinceId is { } since)
                q = q.Where(n => n.Id > since);

            var rows = await q
                .OrderByDescending(n => n.Id)
                .Take(request.Limit)
                .Select(n => new Notification(
                    n.Id, n.Type, n.Title, n.Body, n.RefItemId,
                    n.AreaId, n.Area != null ? n.Area.Name : null, n.CreatedAt))
                .ToListAsync(ct);

            long? latestId = rows.Count > 0 ? rows[0].Id : request.SinceId;
            return Result.Success(new Response(rows, latestId));
        }
    }
}
