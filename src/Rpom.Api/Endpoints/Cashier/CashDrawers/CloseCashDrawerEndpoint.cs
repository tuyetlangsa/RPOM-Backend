using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.CashDrawers.CloseCashDrawer;

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
                var result = await sender.Send(
                    new CloseCashDrawer.Command(id, counts, request.Notes), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.CashDrawerClose)
            .WithTags("CashDrawers")
            .WithName("CloseCashDrawer");
    }

    internal sealed record Request(
        IReadOnlyList<CashCountInput> ClosingCashCounts,
        string? Notes);

    internal sealed record CashCountInput(int DenominationId, int Quantity);
}
