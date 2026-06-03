using MediatR;
using Rpom.Api.Results;
using Rpom.Application.ShiftSessions.GetCurrentShiftSession;

namespace Rpom.Api.Endpoints.System.ShiftSessions;

internal sealed class GetCurrentShiftSessionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/shift-sessions/me", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCurrentShiftSession.Query(), ct);
            return result.MatchOk();
        })
        .RequireAuthorization()
        .WithTags("ShiftSessions")
        .WithName("GetCurrentShiftSession")
        .WithSummary("Return the OPEN shift session of the calling staff, or 404 if none.");
    }
}
