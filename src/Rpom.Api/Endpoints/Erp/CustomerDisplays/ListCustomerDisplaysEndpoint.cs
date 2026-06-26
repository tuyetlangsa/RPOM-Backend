using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.CustomerDisplays.ListCustomerDisplays;

namespace Rpom.Api.Endpoints.Erp.CustomerDisplays;

internal sealed class ListCustomerDisplaysEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/customer-displays",
                async ([FromQuery] int? counterId, [FromQuery] string? search, [FromQuery] bool? isActive,
                    ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new ListCustomerDisplays.Query(counterId, search, isActive), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .Produces<ApiResult<IReadOnlyList<ListCustomerDisplays.Response>>>(StatusCodes.Status200OK)
            .WithTags("CustomerDisplays")
            .WithName("ListCustomerDisplays")
            .WithSummary("List of guest display devices (no DeviceToken required).");
    }
}
