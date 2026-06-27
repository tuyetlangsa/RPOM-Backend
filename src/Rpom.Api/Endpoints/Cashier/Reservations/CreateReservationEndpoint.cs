using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Reservation.CreateReservation;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Reservations;

internal sealed class CreateReservationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/reservations",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<CreateReservation.Response> result = await sender.Send(new CreateReservation.Command(
                        request.CounterId, request.TargetTime, request.CustomerName, request.CustomerPhone,
                        request.GuestCount, request.Note, request.TableIds), ct);
                    return result.MatchCreated(r => $"/api/reservations/{r.ReservationId}");
                })
            .RequireAuthorization(Permissions.ReservationCreate)
            .WithTags("Reservations")
            .WithName("CreateReservation")
            .Produces<ApiResult<CreateReservation.Response>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create a multi-table phone reservation.");
    }

    internal sealed record Request(
        int CounterId, DateTime TargetTime, string CustomerName, string CustomerPhone,
        short GuestCount, string? Note, IReadOnlyList<int> TableIds);
}
