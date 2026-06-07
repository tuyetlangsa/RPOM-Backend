using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Lookups.GetShifts;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Lookups;

internal sealed class GetShiftsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/lookups/shifts", async (ISender sender, CancellationToken ct) =>
            {
                Result<IReadOnlyList<GetShifts.ShiftItem>> result = await sender.Send(new GetShifts.Query(), ct);
                return result.MatchOk();
            })
            .RequireAuthorization()
            .WithTags("Lookups")
            .WithName("GetShifts")
            .Produces<ApiResult<IReadOnlyList<GetShifts.ShiftItem>>>()
            .WithSummary("List shifts for selection lists.")
            .WithDescription("Request: none. Response: 200 OK — JSON array of GetShifts.Response.");
    }
}
