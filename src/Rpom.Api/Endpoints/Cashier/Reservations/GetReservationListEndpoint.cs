using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Reservation.GetReservationList;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Reservations;

internal sealed class GetReservationListEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reservations",
                async (int counterId, DateOnly date, string? status, ISender sender, CancellationToken ct) =>
                {
                    Result<GetReservationList.Response> result =
                        await sender.Send(new GetReservationList.Query(counterId, date, status), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.ReservationView)
            .WithTags("Reservations")
            .WithName("GetReservationList")
            .WithSummary("Counter-scoped, day-filtered reservation list (lazy-expires no-shows).");
    }
}
