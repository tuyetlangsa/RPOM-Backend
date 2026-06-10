using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.CashDrawers.OpenCashDrawer;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.CashDrawers;

internal sealed class OpenCashDrawerEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/cash-drawers",
                async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var counts = request.OpeningCashCounts
                        .Select(c => new OpenCashDrawer.CashCountInput(c.DenominationId, c.Quantity))
                        .ToList();
                    Result<OpenCashDrawer.Response> result = await sender.Send(
                        new OpenCashDrawer.Command(request.CounterId, request.ShiftId, counts, request.Notes), ct);
                    return result.MatchCreated(r => $"/api/cash-drawers/{r.Id}");
                })
            .RequireAuthorization(Permissions.CashDrawerOpen)
            .WithTags("CashDrawers")
            .WithName("OpenCashDrawer")
            .Produces<ApiResult<OpenCashDrawer.Response>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Open a new cash drawer session for a counter.")
            .WithDescription("""
    Request: JSON body { counterId:int, shiftId:int, openingCashCounts:[{ denominationId:int, quantity:int }],
    notes?:string }. Response: 201 Created — Location header; JSON body with new session id.
""");
    }

    internal sealed record Request(
        int CounterId,
        int ShiftId,
        IReadOnlyList<CashCountInput> OpeningCashCounts,
        string? Notes);

    internal sealed record CashCountInput(int DenominationId, int Quantity);
}
