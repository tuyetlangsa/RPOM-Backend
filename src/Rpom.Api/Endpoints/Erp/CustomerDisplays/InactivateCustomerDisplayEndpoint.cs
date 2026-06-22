using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.CustomerDisplays.InactivateCustomerDisplay;

namespace Rpom.Api.Endpoints.Erp.CustomerDisplays;

internal sealed class InactivateCustomerDisplayEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/customer-displays/{id:int}/inactivate",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new InactivateCustomerDisplay.Command(id), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .Produces<ApiResult<InactivateCustomerDisplay.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("CustomerDisplays")
            .WithName("InactivateCustomerDisplay")
            .WithSummary("Disable guest screen (token becomes inactive).");
    }
}
