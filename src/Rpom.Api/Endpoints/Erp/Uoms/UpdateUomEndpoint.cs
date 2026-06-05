using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Uoms.UpdateUom;

namespace Rpom.Api.Endpoints.Erp.Uoms;

internal sealed class UpdateUomEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/uoms/{id:int}",
            async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new UpdateUom.Command(
                    id, request.Code, request.Name, request.Description, request.IsActive), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Uoms")
            .WithName("UpdateUom");
    }

    internal sealed record Request(string Code, string Name, string? Description, bool IsActive);
}
