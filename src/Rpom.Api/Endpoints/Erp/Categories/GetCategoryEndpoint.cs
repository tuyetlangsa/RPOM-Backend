using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Categories.GetCategory;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Categories;

internal sealed class GetCategoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/categories/{id:int}",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    Result<GetCategory.Response> result = await sender.Send(new GetCategory.Query(id), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Categories")
            .WithName("GetCategory")
            .Produces<ApiResult<GetCategory.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a category by id.")
            .WithDescription("Request: route id (int). Response: 200 OK — JSON GetCategory.Response.");
    }
}
