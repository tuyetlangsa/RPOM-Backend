using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.CustomerDisplays.GetCustomerDisplay;

namespace Rpom.Api.Endpoints.Erp.CustomerDisplays;

internal sealed class GetCustomerDisplayEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/customer-displays/{id:int}",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new GetCustomerDisplay.Query(id), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .Produces<ApiResult<GetCustomerDisplay.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("CustomerDisplays")
            .WithName("GetCustomerDisplay")
            .WithSummary("Details of 1 client screen by ID (DeviceToken not provided).");
    }
}
