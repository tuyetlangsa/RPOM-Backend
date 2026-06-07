using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.DiscountPolicies.GetDiscountPolicy;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.DiscountPolicies;

internal sealed class GetDiscountPolicyEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/discount-policies/{id:int}",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    Result<GetDiscountPolicy.Response> result = await sender.Send(new GetDiscountPolicy.Query(id), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("DiscountPolicies")
            .WithName("GetDiscountPolicy")
            .Produces<ApiResult<GetDiscountPolicy.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a discount policy by id (with conditions).")
            .WithDescription(
                "Request: route id (int). Response: 200 OK — JSON GetDiscountPolicy.Response (incl. conditions[]).");
    }
}
