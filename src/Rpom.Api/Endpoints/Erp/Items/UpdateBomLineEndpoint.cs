using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Items.UpdateBomLine;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Items;

internal sealed class UpdateBomLineEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/items/{itemId:int}/bom/{id:int}",
                async (int itemId, int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<UpdateBomLine.Response> result = await sender.Send(new UpdateBomLine.Command(
                        id, itemId, request.MaterialItemId, request.Quantity, request.UomId, request.IsActive), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Items")
            .WithName("UpdateBomLine")
            .Produces<ApiResult<UpdateBomLine.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Update a BOM line.")
            .WithDescription("Request: route itemId (int), id (int); JSON body { materialItemId:int, quantity:decimal, uomId:int, isActive:bool }. Response: 200 OK.");
    }

    internal sealed record Request(int MaterialItemId, decimal Quantity, int UomId, bool IsActive);
}
