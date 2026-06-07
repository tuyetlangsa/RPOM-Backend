using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.SetMenus.UpsertSetMenu;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.SetMenus;

internal sealed class UpsertSetMenuEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/items/{itemId:int}/set-menu",
                async (int itemId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var details = (request.Details ?? Array.Empty<DetailRequest>())
                        .Select(d => new UpsertSetMenu.DetailInput(
                            d.DetailType, d.ComponentItemId, d.Quantity, d.IsFixed,
                            d.ChoiceCategoryId, d.DisplayOrder))
                        .ToList();
                    Result<UpsertSetMenu.Response> result = await sender.Send(
                        new UpsertSetMenu.Command(itemId, request.Description, details), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("SetMenus")
            .WithName("UpsertSetMenu")
            .Produces<ApiResult<UpsertSetMenu.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create or update an item's set-menu spec.")
            .WithDescription(
                "Request: route itemId (int); JSON body { description?:string, details:[{ detailType:'COMPONENT'|'CHOICE_CATEGORY', componentItemId?:int, quantity?:decimal, isFixed?:bool, choiceCategoryId?:int, displayOrder:short }] }. Response: 200 OK — JSON UpsertSetMenu.Response { itemId, detailCount }.");
    }

    internal sealed record Request(string? Description, IReadOnlyList<DetailRequest>? Details);

    internal sealed record DetailRequest(
        string DetailType,
        int? ComponentItemId,
        decimal? Quantity,
        bool? IsFixed,
        int? ChoiceCategoryId,
        short DisplayOrder);
}
