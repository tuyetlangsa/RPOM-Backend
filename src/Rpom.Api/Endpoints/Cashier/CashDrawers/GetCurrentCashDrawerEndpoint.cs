using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.CashDrawers.GetCurrentCashDrawer;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Cashier.CashDrawers;

internal sealed class GetCurrentCashDrawerEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/cash-drawers/current",
                async (int counterId, ISender sender, CancellationToken ct) =>
                {
                    Result<GetCurrentCashDrawer.Response?> result =
                        await sender.Send(new GetCurrentCashDrawer.Query(counterId), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.StaffLogin)
            .WithTags("CashDrawers")
            .WithName("GetCurrentCashDrawer")
            .Produces<ApiResult<GetCurrentCashDrawer.Response>>()
            .WithSummary("Get the open cash drawer session for a counter.")
            .WithDescription("Request: query counterId (int). Response: 200 OK — JSON GetCurrentCashDrawer.Response.");
    }
}
