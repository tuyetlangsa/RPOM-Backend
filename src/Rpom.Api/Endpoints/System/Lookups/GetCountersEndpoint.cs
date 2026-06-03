using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Lookups.GetCounters;

namespace Rpom.Api.Endpoints.System.Lookups;

internal sealed class GetCountersEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/lookups/counters", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCounters.Query(), ct);
            return result.MatchOk();
        })
        .RequireAuthorization()
        .WithTags("Lookups")
        .WithName("GetCounters");
    }
}
