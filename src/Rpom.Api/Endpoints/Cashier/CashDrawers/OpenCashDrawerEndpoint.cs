using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.CashDrawers.OpenCashDrawer;

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
                var result = await sender.Send(
                    new OpenCashDrawer.Command(request.CounterId, counts, request.Notes), ct);
                return result.MatchCreated(r => $"/api/cash-drawers/{r.Id}");
            })
            .RequireAuthorization(Permissions.CashDrawerOpen)
            .WithTags("CashDrawers")
            .WithName("OpenCashDrawer");
    }

    internal sealed record Request(
        int CounterId,
        IReadOnlyList<CashCountInput> OpeningCashCounts,
        string? Notes);

    internal sealed record CashCountInput(int DenominationId, int Quantity);
}
