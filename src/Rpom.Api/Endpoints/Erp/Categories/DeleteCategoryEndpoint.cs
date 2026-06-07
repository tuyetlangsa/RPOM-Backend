using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Categories.DeleteCategory;

namespace Rpom.Api.Endpoints.Erp.Categories;

internal sealed class DeleteCategoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/categories/{id:int}",
            async (int id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new DeleteCategory.Command(id), ct);
                return result.MatchNoContent();
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Categories")
            .WithName("DeleteCategory")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Delete a category.")
            .WithDescription("Request: route id (int). Response: 204 No Content.");
    }
}
