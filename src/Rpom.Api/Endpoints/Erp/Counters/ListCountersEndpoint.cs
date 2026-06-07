using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Counters.ListCounters;

namespace Rpom.Api.Endpoints.Erp.Counters;

internal sealed class ListCountersEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/counters",
            async (string? search, bool? isActive, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new ListCounters.Query(search, isActive), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Counters")
            .WithName("ListCounters")
            .Produces<ApiResult<IReadOnlyList<ListCounters.Response>>>(StatusCodes.Status200OK)
            .WithSummary("List counters with optional search + active filter.")
            .WithDescription("Request: query search?:string, isActive?:bool. Response: 200 OK — JSON array of ListCounters.Response.");
    }
}
