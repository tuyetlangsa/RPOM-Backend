using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Uoms.DeleteUom;

namespace Rpom.Api.Endpoints.Erp.Uoms;

internal sealed class DeleteUomEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/uoms/{id:int}",
            async (int id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new DeleteUom.Command(id), ct);
                return result.MatchNoContent();
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Uoms")
            .WithName("DeleteUom");
    }
}
