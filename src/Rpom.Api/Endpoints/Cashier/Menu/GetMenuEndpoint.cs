using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.GetMenu;

namespace Rpom.Api.Endpoints.Cashier.Menu;

internal sealed class GetMenuEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/cashier/menu",
            async (int tableId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetMenu.Query(tableId), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.CashierViewMenu)
            .WithTags("Menu")
            .WithName("GetMenu");
    }
}
