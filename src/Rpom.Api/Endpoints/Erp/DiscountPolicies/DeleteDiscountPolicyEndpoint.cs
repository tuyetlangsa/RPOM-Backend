using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.DiscountPolicies.DeleteDiscountPolicy;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.DiscountPolicies;

internal sealed class DeleteDiscountPolicyEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/discount-policies/{id:int}",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    Result result = await sender.Send(new DeleteDiscountPolicy.Command(id), ct);
                    return result.MatchNoContent();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("DiscountPolicies")
            .WithName("DeleteDiscountPolicy")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Delete a discount policy.")
            .WithDescription("Request: route id (int). Response: 204 No Content. 409 if still referenced by a ticket.");
    }
}
