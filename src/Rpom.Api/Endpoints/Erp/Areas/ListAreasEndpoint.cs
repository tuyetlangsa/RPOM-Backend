using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Areas.ListAreas;

namespace Rpom.Api.Endpoints.Erp.Areas;

internal sealed class ListAreasEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/areas",
            async (int? counterId, string? search, bool? isActive, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new ListAreas.Query(counterId, search, isActive), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Areas")
            .WithName("ListAreas");
    }
}
