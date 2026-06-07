using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.ChoiceCategories.ListChoiceCategories;

namespace Rpom.Api.Endpoints.Erp.ChoiceCategories;

internal sealed class ListChoiceCategoriesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/choice-categories",
            async (string? search, bool? isActive, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new ListChoiceCategories.Query(search, isActive), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("ChoiceCategories")
            .WithName("ListChoiceCategories")
            .Produces<ApiResult<IReadOnlyList<ListChoiceCategories.Response>>>(StatusCodes.Status200OK)
            .WithSummary("List choice categories with optional filters.")
            .WithDescription("Request: query search?:string, isActive?:bool. Response: 200 OK — JSON array of ListChoiceCategories.Response (incl. modifierCount).");
    }
}
