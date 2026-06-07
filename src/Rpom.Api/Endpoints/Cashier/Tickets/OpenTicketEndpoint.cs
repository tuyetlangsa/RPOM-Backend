using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.OpenTicket;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Tickets;

internal sealed class OpenTicketEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/cashier/tickets",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    Result<OpenTicket.Response> result = await sender.Send(new OpenTicket.Command(
                        request.TableId, request.GuestCount, request.ShiftId, request.Notes), ct);
                    return result.MatchCreated(r => $"/api/cashier/tickets/{r.TicketId}");
                })
            .RequireAuthorization(Permissions.TicketOpen)
            .WithTags("Tickets")
            .WithName("OpenTicket")
            .Produces<ApiResult<OpenTicket.Response>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Open a new ticket on a table.")
            .WithDescription(
                "Request: JSON body { tableId:int, guestCount:short, shiftId:int, notes?:string }. Requires the table lock; counter/area derived from the table. Response: 201 Created — Location header; JSON body { ticketId, code }. 409 if no open cash drawer / lock not held.");
    }

    internal sealed record Request(int TableId, short GuestCount, int ShiftId, string? Notes);
}
