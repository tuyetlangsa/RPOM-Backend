using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Uoms.ListUoms;

namespace Rpom.Api.Endpoints.Erp.Uoms;

internal sealed class ListUomsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/uoms",
            async (string? search, bool? isActive, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new ListUoms.Query(search, isActive), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Uoms")
            .WithName("ListUoms")
            .Produces<ApiResult<IReadOnlyList<ListUoms.Response>>>(StatusCodes.Status200OK)
            .WithSummary("List units of measure with optional filters.")
            .WithDescription("Request: query search?:string, isActive?:bool. Response: 200 OK — JSON array of ListUoms.Response.");
    }
}
