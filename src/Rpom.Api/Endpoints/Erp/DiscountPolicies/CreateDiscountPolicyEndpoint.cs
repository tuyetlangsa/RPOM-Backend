using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.DiscountPolicies.CreateDiscountPolicy;

namespace Rpom.Api.Endpoints.Erp.DiscountPolicies;

internal sealed class CreateDiscountPolicyEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/discount-policies",
            async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var conditions = (request.Conditions ?? Array.Empty<ConditionRequest>())
                    .Select(c => new CreateDiscountPolicy.ConditionInput(
                        c.ThresholdAmount, c.ItemId, c.QuantityThreshold, c.AreaId,
                        c.ApplyType, c.DiscountValue, c.DisplayOrder))
                    .ToList();
                var result = await sender.Send(new CreateDiscountPolicy.Command(
                    request.Code, request.Name, request.Description, request.DiscountType,
                    request.IsAutoApply, request.DaysOfWeek, request.IsActive, conditions), ct);
                return result.MatchCreated(r => $"/api/discount-policies/{r.Id}");
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("DiscountPolicies")
            .WithName("CreateDiscountPolicy")
            .Produces<ApiResult<CreateDiscountPolicy.Response>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create a discount policy with its conditions.")
            .WithDescription("Request: JSON body { code:string, name:string, description?:string, discountType:'TICKET_THRESHOLD'|'QUANTITY_ITEM', isAutoApply:bool, daysOfWeek?:string, isActive:bool, conditions:[{ thresholdAmount?:decimal, itemId?:int, quantityThreshold?:decimal, areaId?:int, applyType:'PERCENT'|'FIXED', discountValue:decimal, displayOrder:short }] }. Response: 201 Created — Location header; JSON body with new policy id.");
    }

    internal sealed record Request(
        string Code, string Name, string? Description, string DiscountType,
        bool IsAutoApply, string? DaysOfWeek, bool IsActive,
        IReadOnlyList<ConditionRequest>? Conditions);

    internal sealed record ConditionRequest(
        decimal? ThresholdAmount, int? ItemId, decimal? QuantityThreshold, int? AreaId,
        string ApplyType, decimal DiscountValue, short DisplayOrder);
}
