using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Reservation.GetReservationFloorPlanProjection;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Reservations;

internal sealed class GetReservationFloorPlanProjectionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reservations/projection",
                async (int counterId, DateTime targetTime, ISender sender, CancellationToken ct) =>
                {
                    Result<GetReservationFloorPlanProjection.Response> result =
                        await sender.Send(new GetReservationFloorPlanProjection.Query(counterId, targetTime), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.ReservationCreate)
            .WithTags("Reservations")
            .WithName("GetReservationFloorPlanProjection")
            .WithSummary("Floor plan of a counter projected to a target booking time (UC-R5).");
    }
}
