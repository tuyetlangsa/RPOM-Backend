using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Items.DeleteItem;

namespace Rpom.Api.Endpoints.Erp.Items;

internal sealed class DeleteItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/items/{id:int}",
            async (int id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new DeleteItem.Command(id), ct);
                return result.MatchNoContent();
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Items")
            .WithName("DeleteItem");
    }
}
