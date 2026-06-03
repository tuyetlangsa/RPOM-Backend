using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.ShiftSessions.CloseShiftSession;

namespace Rpom.Api.Endpoints.System.ShiftSessions;

internal sealed class CloseShiftSessionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/shift-sessions/{id:long}/close",
            async (long id, [FromBody] Request? request, ISender sender, CancellationToken ct) =>
        {
            var counts = request?.ClosingCashCounts?
                .Select(c => new CloseShiftSession.CashCountInput(c.DenominationId, c.Quantity))
                .ToList();

            var result = await sender.Send(new CloseShiftSession.Command(id, counts), ct);
            return result.MatchOk();
        })
        .RequireAuthorization(Permissions.ShiftSessionClose)
        .WithTags("ShiftSessions")
        .WithName("CloseShiftSession");
    }

    internal sealed record CashCountDto(int DenominationId, int Quantity);
    internal sealed record Request(IReadOnlyList<CashCountDto>? ClosingCashCounts);
}
