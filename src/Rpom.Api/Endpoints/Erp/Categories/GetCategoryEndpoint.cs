using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Categories.GetCategory;

namespace Rpom.Api.Endpoints.Erp.Categories;

internal sealed class GetCategoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/categories/{id:int}",
            async (int id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetCategory.Query(id), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Categories")
            .WithName("GetCategory");
    }
}
