using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Lookups.GetBomMaterials;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Lookups;

internal sealed class GetBomMaterialsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/lookups/bom-materials", async (
                string? search, ISender sender, CancellationToken ct) =>
            {
                Result<IReadOnlyList<GetBomMaterials.MaterialItem>> result =
                    await sender.Send(new GetBomMaterials.Query(search), ct);
                return result.MatchOk();
            })
            .RequireAuthorization()
            .WithTags("Lookups")
            .WithName("GetBomMaterials")
            .Produces<ApiResult<IReadOnlyList<GetBomMaterials.MaterialItem>>>()
            .WithSummary("List items usable as a BOM material (IsStockable=true AND HasRecipe=false).")
            .WithDescription(
                "Request: optional ?search= (code/name). Response: 200 OK — ingredient items valid as a "
                + "BOM material, each with its base UoM for the quantity unit selector.");
    }
}
