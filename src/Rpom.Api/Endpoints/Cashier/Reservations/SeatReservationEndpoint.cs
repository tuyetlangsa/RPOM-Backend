using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Reservation.SeatReservation;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Reservations;

internal sealed class SeatReservationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/reservations/{reservationId:long}/seat",
                async (long reservationId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var tables = request.Tables
                        .Select(t => new SeatReservation.SeatTable(t.TableId, t.GuestCount)).ToList();
                    Result<SeatReservation.Response> result =
                        await sender.Send(new SeatReservation.Command(reservationId, tables), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.ReservationSeat)
            .WithTags("Reservations")
            .WithName("SeatReservation")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Seat a reservation: open one ticket per selected table (UC-R3).");
    }

    internal sealed record Request(IReadOnlyList<SeatTableRequest> Tables);
    internal sealed record SeatTableRequest(int TableId, short GuestCount);
}
