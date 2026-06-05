using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Items.ListItems;

namespace Rpom.Api.Endpoints.Erp.Items;

internal sealed class ListItemsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/items",
            async (int? categoryId, string? search, bool? isActive, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new ListItems.Query(categoryId, search, isActive), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Items")
            .WithName("ListItems");
    }
}
