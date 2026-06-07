using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.GetMenu;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.Menu;

internal sealed class GetMenuEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/cashier/menu",
                async (int tableId, ISender sender, CancellationToken ct) =>
                {
                    Result<GetMenu.Response> result = await sender.Send(new GetMenu.Query(tableId), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.CashierViewMenu)
            .WithTags("Menu")
            .WithName("GetMenu")
            .Produces<ApiResult<GetMenu.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get menu tree with resolved prices for a table.")
            .WithDescription(
                "Request: query tableId (int). Response: 200 OK — JSON GetMenu.Response (category tree + priced items; most-specific price wins).");
    }
}
