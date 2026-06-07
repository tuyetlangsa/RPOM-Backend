using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.ChoiceCategories.GetChoiceCategory;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.ChoiceCategories;

internal sealed class GetChoiceCategoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/choice-categories/{id:int}",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    Result<GetChoiceCategory.Response> result = await sender.Send(new GetChoiceCategory.Query(id), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("ChoiceCategories")
            .WithName("GetChoiceCategory")
            .Produces<ApiResult<GetChoiceCategory.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a choice category by id (with modifiers).")
            .WithDescription(
                "Request: route id (int). Response: 200 OK — JSON GetChoiceCategory.Response (incl. modifiers[]).");
    }
}
