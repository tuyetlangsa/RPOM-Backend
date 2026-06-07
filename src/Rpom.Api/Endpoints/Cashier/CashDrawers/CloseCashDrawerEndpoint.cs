using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.CashDrawers.CloseCashDrawer;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.CashDrawers;

internal sealed class CloseCashDrawerEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/cash-drawers/{id:long}/close",
                async (long id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var counts = request.ClosingCashCounts
                        .Select(c => new CloseCashDrawer.CashCountInput(c.DenominationId, c.Quantity))
                        .ToList();
                    Result<CloseCashDrawer.Response> result = await sender.Send(
                        new CloseCashDrawer.Command(id, counts, request.Notes), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.CashDrawerClose)
            .WithTags("CashDrawers")
            .WithName("CloseCashDrawer")
            .Produces<ApiResult<CloseCashDrawer.Response>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Close a cash drawer session with counted cash.")
            .WithDescription(
                "Request: route id (long); JSON body { closingCashCounts:[{ denominationId:int, quantity:int }], notes?:string }. Response: 200 OK — JSON CloseCashDrawer.Response.");
    }

    internal sealed record Request(
        IReadOnlyList<CashCountInput> ClosingCashCounts,
        string? Notes);

    internal sealed record CashCountInput(int DenominationId, int Quantity);
}
