using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Items.CreateUomConversion;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Items;

internal sealed class CreateUomConversionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/items/{itemId:int}/uom-conversions",
                async (int itemId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<CreateUomConversion.Response> result = await sender.Send(new CreateUomConversion.Command(
                        itemId, request.UomId, request.FactorToBase, request.IsActive), ct);
                    return result.MatchCreated(u => $"/api/items/{itemId}/uom-conversions/{u.Id}");
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Items")
            .WithName("CreateUomConversion")
            .Produces<ApiResult<CreateUomConversion.Response>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create an item UOM conversion.")
            .WithDescription("Request: route itemId (int); JSON body { uomId:int, factorToBase:decimal, isActive:bool }. Response: 201 Created.");
    }

    internal sealed record Request(int UomId, decimal FactorToBase, bool IsActive);
}
