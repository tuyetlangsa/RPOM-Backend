using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Lookups.GetCounters;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Lookups;

internal sealed class GetCountersEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/lookups/counters", async (ISender sender, CancellationToken ct) =>
            {
                Result<IReadOnlyList<GetCounters.CounterItem>> result = await sender.Send(new GetCounters.Query(), ct);
                return result.MatchOk();
            })
            .RequireAuthorization()
            .WithTags("Lookups")
            .WithName("GetCounters")
            .Produces<ApiResult<IReadOnlyList<GetCounters.CounterItem>>>()
            .WithSummary("List counters for selection lists.")
            .WithDescription("Request: none. Response: 200 OK — JSON array of GetCounters.Response.");
    }
}
