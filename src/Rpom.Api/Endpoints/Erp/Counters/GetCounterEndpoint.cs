using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Counters.GetCounter;

namespace Rpom.Api.Endpoints.Erp.Counters;

internal sealed class GetCounterEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/counters/{id:int}",
            async (int id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetCounter.Query(id), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Counters")
            .WithName("GetCounter");
    }
}
