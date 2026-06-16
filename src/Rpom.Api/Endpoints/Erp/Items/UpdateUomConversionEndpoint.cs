using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Items.UpdateUomConversion;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Items;

internal sealed class UpdateUomConversionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/items/{itemId:int}/uom-conversions/{id:int}",
                async (int itemId, int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<UpdateUomConversion.Response> result = await sender.Send(new UpdateUomConversion.Command(
                        id, itemId, request.UomId, request.FactorToBase, request.IsActive), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Items")
            .WithName("UpdateUomConversion")
            .Produces<ApiResult<UpdateUomConversion.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Update an item UOM conversion.")
            .WithDescription("Request: route itemId (int), id (int); JSON body { uomId:int, factorToBase:decimal, isActive:bool }. Response: 200 OK.");
    }

    internal sealed record Request(int UomId, decimal FactorToBase, bool IsActive);
}
