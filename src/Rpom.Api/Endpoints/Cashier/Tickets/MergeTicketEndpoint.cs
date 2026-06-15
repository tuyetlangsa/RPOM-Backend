using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.MergeTicket;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class MergeTicketEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/cashier/tickets/merge",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(
                        new MergeTicket.Command(request.SourceTicketId, request.DestinationTicketId), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.TicketMerge)
            .Produces<ApiResult<MergeTicket.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("Tickets")
            .WithName("MergeTicket")
            .WithSummary("Merge a source ticket into a destination ticket (same area); the source ticket is updated to CANCELLED.");
    }

    internal sealed record Request(long SourceTicketId, long DestinationTicketId);
}
