using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.PriceVariants.UpdatePriceVariant;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.PriceVariants;

internal sealed class UpdatePriceVariantEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/price-variants/{id:int}",
                async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<UpdatePriceVariant.Response> result = await sender.Send(new UpdatePriceVariant.Command(
                        id, request.Code, request.Name, request.Description,
                        request.BeginTime, request.EndTime, request.DayMask,
                        request.AppliesToAllAreas, request.AreaIds ?? Array.Empty<int>(),
                        request.IsActive), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("PriceVariants")
            .WithName("UpdatePriceVariant")
            .Produces<ApiResult<UpdatePriceVariant.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Update a price variant.")
            .WithDescription("""
    Request: route id (int); JSON body { code:string, name:string, description?:string, beginTime?:time,
    endTime?:time, dayMask?:int, appliesToAllAreas:bool, areaIds?:int[], isActive:bool }. Response: 200
    OK — JSON UpdatePriceVariant.Response.
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
