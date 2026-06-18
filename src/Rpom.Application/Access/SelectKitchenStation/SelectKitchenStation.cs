using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Authentication;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;

namespace Rpom.Application.Access.SelectKitchenStation;

/// <summary>
///     Chọn khu bếp (kitchen station) cho phiên đăng nhập hiện tại và cấp lại JWT
///     có kèm claim <c>kitchen_station_id</c>. Sau bước này, các màn hình bếp (KDS)
///     đọc station từ token thay vì truyền tham số. Tương tự mô hình chọn Counter.
///     Yêu cầu đã đăng nhập + quyền <c>kds:view</c> (enforced ở endpoint).
/// </summary>
public static class SelectKitchenStation
{
    public sealed record Command(int KitchenStationId) : ICommand<Response>;

    public sealed record Response(
        string AccessToken,
        DateTime ExpiresAt,
        int KitchenStationId,
        string KitchenStationName);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.KitchenStationId).GreaterThan(0);
        }
    }

    internal sealed class Handler(
        IDbContext dbContext,
        ICurrentStaff currentStaff,
        IJwtTokenService jwtTokenService) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken ct)
        {
            var station = await dbContext.KitchenStations
                .Where(s => s.Id == request.KitchenStationId && s.IsActive)
                .Select(s => new { s.Id, s.Name })
                .FirstOrDefaultAsync(ct);
            if (station is null)
                return Result.Failure<Response>(KitchenStationErrors.NotFound);

            AccessTokenResult token = jwtTokenService.IssueAccessToken(
                currentStaff.StaffAccountId, currentStaff.Username, station.Id);

            return Result.Success(new Response(
                token.Token, token.ExpiresAt, station.Id, station.Name));
        }
    }
}
