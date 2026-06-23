using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.CustomerDisplays.UpdateCustomerDisplay;

namespace Rpom.Api.Endpoints.Erp.CustomerDisplays;

internal sealed class UpdateCustomerDisplayEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/customer-displays/{id:int}",
                async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(
                        new UpdateCustomerDisplay.Command(id, request.Name, request.IdleMediaUrl), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataManage)
            .Produces<ApiResult<UpdateCustomerDisplay.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("CustomerDisplays")
            .WithName("UpdateCustomerDisplay")
            .WithSummary("Rename + non-working image/video of the guest screen. ");
    }

    internal sealed record Request(string Name, string? IdleMediaUrl);
}
