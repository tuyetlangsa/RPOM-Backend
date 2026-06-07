using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.ApplyDiscountPolicy;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class ApplyDiscountPolicyEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/cashier/tickets/{ticketId:long}/discount-policy",
            async (long ticketId, [FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(
                    new ApplyDiscountPolicy.Command(ticketId, request.DiscountPolicyId), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.OrderSendKitchen)
            .WithTags("Tickets")
            .WithName("ApplyDiscountPolicy")
            .Produces<ApiResult<ApplyDiscountPolicy.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Apply a discount policy to the ticket.")
            .WithDescription("""
    Request: route ticketId (long); JSON body { discountPolicyId:int }. Evaluates policy conditions
    against the ticket; picks the best match. PERCENT: sets Ticket.DiscountPercent or per-line
    LineDiscountPercent. FIXED: distributes amount across lines proportionally. One policy at a time —
    remove first to switch. Response: 200 OK — JSON body { ticketId, discountAmount, totalAmount }.
""");
    }

    internal sealed record Request(int DiscountPolicyId);
}
