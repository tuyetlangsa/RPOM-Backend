using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.KitchenStations.ListKitchenStations;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.KitchenStations;

internal sealed class ListKitchenStationsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/kitchen-stations",
                async (string? search, bool? isActive, ISender sender, CancellationToken ct) =>
                {
                    Result<IReadOnlyList<ListKitchenStations.Response>> result =
                        await sender.Send(new ListKitchenStations.Query(search, isActive), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("KitchenStations")
            .WithName("ListKitchenStations")
            .Produces<ApiResult<IReadOnlyList<ListKitchenStations.Response>>>()
            .WithSummary("List kitchen stations with optional filters.")
            .WithDescription(
                "Request: query search?:string, isActive?:bool. Response: 200 OK — JSON array of ListKitchenStations.Response.");
    }
}
