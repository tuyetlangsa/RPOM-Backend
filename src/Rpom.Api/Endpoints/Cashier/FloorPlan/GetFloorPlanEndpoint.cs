using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Cashier.GetFloorPlan;

namespace Rpom.Api.Endpoints.Cashier.FloorPlan;

internal sealed class GetFloorPlanEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/cashier/floor-plan",
            async (int counterId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetFloorPlan.Query(counterId), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.CashierFloorPlan)
            .WithTags("Areas")
            .WithName("GetFloorPlan");
    }
}
