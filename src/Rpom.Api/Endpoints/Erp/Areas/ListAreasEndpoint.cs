using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Areas.ListAreas;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Areas;

internal sealed class ListAreasEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/areas",
                async (int? counterId, string? search, bool? isActive, ISender sender, CancellationToken ct) =>
                {
                    Result<IReadOnlyList<ListAreas.Response>> result =
                        await sender.Send(new ListAreas.Query(counterId, search, isActive), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Areas")
            .WithName("ListAreas")
            .Produces<ApiResult<IReadOnlyList<ListAreas.Response>>>()
            .WithSummary("List areas with optional filters.")
            .WithDescription(
                "Request: query counterId?:int, search?:string, isActive?:bool. Response: 200 OK — JSON array of ListAreas.Response.");
    }
}
