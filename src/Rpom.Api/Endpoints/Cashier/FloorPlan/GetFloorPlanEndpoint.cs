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
            .WithName("GetFloorPlan")
            .Produces<ApiResult<GetFloorPlan.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get cashier floor plan (areas + tables + ticket summary) for a counter.")
            .WithDescription("Request: query counterId (int). Response: 200 OK — JSON GetFloorPlan.Response (areas to tables with status + latest ticket).");
    }
}
