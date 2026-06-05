using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.CashDrawers.GetCurrentCashDrawer;

namespace Rpom.Api.Endpoints.Cashier.CashDrawers;

internal sealed class GetCurrentCashDrawerEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/cash-drawers/current",
            async (int counterId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetCurrentCashDrawer.Query(counterId), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.StaffLogin)
            .WithTags("CashDrawers")
            .WithName("GetCurrentCashDrawer");
    }
}
