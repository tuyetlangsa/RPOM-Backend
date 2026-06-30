using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Reservation.CancelReservation;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Reservations;

internal sealed class CancelReservationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/reservations/{reservationId:long}/cancel",
                async (long reservationId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result result = await sender.Send(
                        new CancelReservation.Command(reservationId, request.CancellationReasonId, request.Note), ct);
                    return result.MatchNoContent();
                })
            .RequireAuthorization(Permissions.ReservationCancel)
            .WithTags("Reservations")
            .WithName("CancelReservation")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Cancel a BOOKED reservation with a reason (UC-R4).");
    }

    internal sealed record Request(int CancellationReasonId, string? Note);
}
