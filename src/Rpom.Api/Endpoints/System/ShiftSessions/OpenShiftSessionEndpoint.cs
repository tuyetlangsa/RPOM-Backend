using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.ShiftSessions.OpenShiftSession;

namespace Rpom.Api.Endpoints.System.ShiftSessions;

internal sealed class OpenShiftSessionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/shift-sessions", async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
        {
            var counts = request.OpeningCashCounts?
                .Select(c => new OpenShiftSession.CashCountInput(c.DenominationId, c.Quantity))
                .ToList();

            var result = await sender.Send(new OpenShiftSession.Command(
                request.ShiftId,
                request.CounterId,
                request.KitchenStationId,
                request.HasCashTracking,
                counts), ct);

            return result.MatchOk();
        })
        .RequireAuthorization(Permissions.ShiftSessionOpen)
        .WithTags("ShiftSessions")
        .WithName("OpenShiftSession");
    }

    internal sealed record CashCountDto(int DenominationId, int Quantity);

    internal sealed record Request(
        int ShiftId,
        int? CounterId,
        int? KitchenStationId,
        bool HasCashTracking,
        IReadOnlyList<CashCountDto>? OpeningCashCounts);
}
