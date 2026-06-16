using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Inventory.CreateStockMovement;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Inventory;

internal sealed class CreateStockMovementEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/stock-movements",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<CreateStockMovement.Response> result =
                        await sender.Send(new CreateStockMovement.Command(
                            request.ItemId, request.MovementType, request.Quantity, request.UomId, request.Reason), ct);
                    return result.MatchCreated(m => $"/api/stock-movements/{m.Id}");
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Inventory")
            .WithName("CreateStockMovement")
            .Produces<ApiResult<CreateStockMovement.Response>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create a manual stock movement (STOCK_IN / ADJUST_IN / ADJUST_OUT).")
            .WithDescription("""
                Request: JSON body { itemId, movementType, quantity, uomId, reason? }.
                Quantity must be > 0 and is expressed in uomId; the server converts to the item's
                base UoM via ItemUomConversion (uomId may be the base UoM or a registered conversion).
                movementType is converted to signed internally.
                Response: 201 Created — Location header; JSON body with input qty/uom + qtyInBase.
            """);
    }

    internal sealed record Request(int ItemId, string MovementType, decimal Quantity, int UomId, string? Reason);
}
