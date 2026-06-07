using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.ChoiceCategories.UpdateChoiceCategory;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.ChoiceCategories;

internal sealed class UpdateChoiceCategoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/choice-categories/{id:int}",
                async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<UpdateChoiceCategory.Response> result = await sender.Send(new UpdateChoiceCategory.Command(
                        id, request.Name, request.Note, request.MinChoice, request.MaxChoice,
                        request.DisplayOrder, request.IsActive), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("ChoiceCategories")
            .WithName("UpdateChoiceCategory")
            .Produces<ApiResult<UpdateChoiceCategory.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Update a choice category.")
            .WithDescription("""
    Request: route id (int); JSON body { name:string, note?:string, minChoice:short, maxChoice?:short,
    displayOrder:short, isActive:bool }. Response: 200 OK — JSON UpdateChoiceCategory.Response.
""");
    }

    internal sealed record Request(
        string Name,
        string? Note,
        short MinChoice,
        short? MaxChoice,
        short DisplayOrder,
        bool IsActive);
}
