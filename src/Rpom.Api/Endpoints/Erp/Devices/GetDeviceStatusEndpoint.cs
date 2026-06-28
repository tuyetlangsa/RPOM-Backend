using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Devices.GetDeviceStatus;

namespace Rpom.Api.Endpoints.Erp.Devices;

internal sealed class GetDeviceStatusEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/devices/status",
                async ([FromQuery] int? counterId, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new GetDeviceStatus.Query(counterId), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .Produces<ApiResult<GetDeviceStatus.Response>>(StatusCodes.Status200OK)
            .WithTags("Devices")
            .WithName("GetDeviceStatus")
            .WithSummary("POS machine and customer monitor: online/offline via LastSeenAt.");
    }
}
