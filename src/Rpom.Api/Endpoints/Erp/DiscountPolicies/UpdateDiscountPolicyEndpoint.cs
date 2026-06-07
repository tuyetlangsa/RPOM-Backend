using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.DiscountPolicies.UpdateDiscountPolicy;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.DiscountPolicies;

internal sealed class UpdateDiscountPolicyEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/discount-policies/{id:int}",
                async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var conditions = (request.Conditions ?? Array.Empty<ConditionRequest>())
                        .Select(c => new UpdateDiscountPolicy.ConditionInput(
                            c.ThresholdAmount, c.ItemId, c.QuantityThreshold, c.AreaId,
                            c.ApplyType, c.DiscountValue, c.DisplayOrder))
                        .ToList();
                    Result<UpdateDiscountPolicy.Response> result = await sender.Send(new UpdateDiscountPolicy.Command(
                        id, request.Code, request.Name, request.Description, request.DiscountType,
                        request.IsAutoApply, request.DaysOfWeek, request.IsActive, conditions), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("DiscountPolicies")
            .WithName("UpdateDiscountPolicy")
            .Produces<ApiResult<UpdateDiscountPolicy.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Update a discount policy (replace-all conditions).")
            .WithDescription("""
    Request: route id (int); JSON body { code:string, name:string, description?:string,
    discountType:'TICKET_THRESHOLD'|'QUANTITY_ITEM', isAutoApply:bool, daysOfWeek?:string,
    isActive:bool, conditions:[{ thresholdAmount?:decimal, itemId?:int, quantityThreshold?:decimal,
    areaId?:int, applyType:'PERCENT'|'FIXED', discountValue:decimal, displayOrder:short }] }. Response:
    200 OK — JSON UpdateDiscountPolicy.Response.
""");
    }

    internal sealed record Request(
        string Code,
        string Name,
        string? Description,
        string DiscountType,
        bool IsAutoApply,
        string? DaysOfWeek,
        bool IsActive,
        IReadOnlyList<ConditionRequest>? Conditions);

    internal sealed record ConditionRequest(
        decimal? ThresholdAmount,
        int? ItemId,
        decimal? QuantityThreshold,
        int? AreaId,
        string ApplyType,
        decimal DiscountValue,
        short DisplayOrder);
}
