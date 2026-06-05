using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Tables.DeleteTable;

namespace Rpom.Api.Endpoints.Erp.Tables;

internal sealed class DeleteTableEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/tables/{id:int}",
            async (int id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new DeleteTable.Command(id), ct);
                return result.MatchNoContent();
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Tables")
            .WithName("DeleteTable");
    }
}
