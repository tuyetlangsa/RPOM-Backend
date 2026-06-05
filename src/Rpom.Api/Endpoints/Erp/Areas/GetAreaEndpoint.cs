using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Areas.GetArea;

namespace Rpom.Api.Endpoints.Erp.Areas;

internal sealed class GetAreaEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/areas/{id:int}",
            async (int id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetArea.Query(id), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Areas")
            .WithName("GetArea");
    }
}
