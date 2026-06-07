using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Lookups.GetShifts;

namespace Rpom.Api.Endpoints.System.Lookups;

internal sealed class GetShiftsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/lookups/shifts", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetShifts.Query(), ct);
            return result.MatchOk();
        })
        .RequireAuthorization()
        .WithTags("Lookups")
        .WithName("GetShifts")
        .Produces<ApiResult<IReadOnlyList<GetShifts.ShiftItem>>>(StatusCodes.Status200OK)
        .WithSummary("List shifts for selection lists.")
        .WithDescription("Request: none. Response: 200 OK — JSON array of GetShifts.Response.");
    }
}
