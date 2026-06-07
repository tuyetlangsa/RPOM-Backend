using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.ChoiceCategories.CreateChoiceCategory;

namespace Rpom.Api.Endpoints.Erp.ChoiceCategories;

internal sealed class CreateChoiceCategoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/choice-categories",
            async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateChoiceCategory.Command(
                    request.Name, request.Note, request.MinChoice, request.MaxChoice,
                    request.DisplayOrder, request.IsActive), ct);
                return result.MatchCreated(r => $"/api/choice-categories/{r.Id}");
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("ChoiceCategories")
            .WithName("CreateChoiceCategory")
            .Produces<ApiResult<CreateChoiceCategory.Response>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create a choice category.")
            .WithDescription("Request: JSON body { name:string, note?:string, minChoice:short, maxChoice?:short, displayOrder:short, isActive:bool }. Response: 201 Created — Location header; JSON body with new choice-category id.");
    }

    internal sealed record Request(
        string Name, string? Note, short MinChoice, short? MaxChoice, short DisplayOrder, bool IsActive);
}
