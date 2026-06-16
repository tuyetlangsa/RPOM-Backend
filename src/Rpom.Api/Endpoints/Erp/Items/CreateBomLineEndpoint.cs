using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Items.CreateBomLine;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Items;

internal sealed class CreateBomLineEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/items/{itemId:int}/bom",
                async (int itemId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<CreateBomLine.Response> result = await sender.Send(new CreateBomLine.Command(
                        itemId, request.MaterialItemId, request.Quantity, request.UomId, request.IsActive), ct);
                    return result.MatchCreated(u => $"/api/items/{itemId}/bom/{u.Id}");
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Items")
            .WithName("CreateBomLine")
            .Produces<ApiResult<CreateBomLine.Response>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create a BOM line (recipe material).")
            .WithDescription("Request: route itemId (int); JSON body { materialItemId:int, quantity:decimal, uomId:int, isActive:bool }. Response: 201 Created.");
    }

    internal sealed record Request(int MaterialItemId, decimal Quantity, int UomId, bool IsActive);
}
