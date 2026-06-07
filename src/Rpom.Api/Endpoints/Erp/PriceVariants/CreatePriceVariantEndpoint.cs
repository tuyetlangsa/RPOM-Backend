using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PriceVariants.CreatePriceVariant;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.PriceVariants;

internal sealed class CreatePriceVariantEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/price-tables/{priceTableId:int}/variants",
                async (int priceTableId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<CreatePriceVariant.Response> result = await sender.Send(new CreatePriceVariant.Command(
                        priceTableId, request.Code, request.Name, request.Description,
                        request.BeginTime, request.EndTime, request.DayMask,
                        request.AppliesToAllAreas, request.AreaIds ?? Array.Empty<int>(),
                        request.IsActive), ct);
                    return result.MatchCreated(r => $"/api/price-variants/{r.Id}");
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("PriceVariants")
            .WithName("CreatePriceVariant")
            .Produces<ApiResult<CreatePriceVariant.Response>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create a price variant under a price table.")
            .WithDescription("""
    Request: route priceTableId (int); JSON body { code:string, name:string, description?:string,
    beginTime?:time, endTime?:time, dayMask?:int, appliesToAllAreas:bool, areaIds?:int[], isActive:bool
    }. Response: 201 Created — Location header; JSON body with new price-variant id.
""");
    }

    internal sealed record Request(
        string Code,
        string Name,
        string? Description,
        TimeOnly? BeginTime,
        TimeOnly? EndTime,
        int? DayMask,
        bool AppliesToAllAreas,
        IReadOnlyList<int>? AreaIds,
        bool IsActive);
}
