using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.ChoiceCategories.DeleteChoiceCategory;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.ChoiceCategories;

internal sealed class DeleteChoiceCategoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/choice-categories/{id:int}",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    Result result = await sender.Send(new DeleteChoiceCategory.Command(id), ct);
                    return result.MatchNoContent();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("ChoiceCategories")
            .WithName("DeleteChoiceCategory")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Delete a choice category.")
            .WithDescription(
                "Request: route id (int). Response: 204 No Content. 409 if still referenced by a set menu.");
    }
}
