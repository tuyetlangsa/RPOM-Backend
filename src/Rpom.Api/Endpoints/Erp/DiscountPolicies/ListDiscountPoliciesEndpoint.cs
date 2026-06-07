using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.DiscountPolicies.ListDiscountPolicies;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.DiscountPolicies;

internal sealed class ListDiscountPoliciesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/discount-policies",
                async (string? search, bool? isActive, string? discountType, ISender sender, CancellationToken ct) =>
                {
                    Result<IReadOnlyList<ListDiscountPolicies.Response>> result =
                        await sender.Send(new ListDiscountPolicies.Query(search, isActive, discountType), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("DiscountPolicies")
            .WithName("ListDiscountPolicies")
            .Produces<ApiResult<IReadOnlyList<ListDiscountPolicies.Response>>>()
            .WithSummary("List discount policies with optional filters.")
            .WithDescription("""
    Request: query search?:string, isActive?:bool, discountType?:'TICKET_THRESHOLD'|'QUANTITY_ITEM'.
    Response: 200 OK — JSON array of ListDiscountPolicies.Response (incl. conditionCount).
""");
    }
}
