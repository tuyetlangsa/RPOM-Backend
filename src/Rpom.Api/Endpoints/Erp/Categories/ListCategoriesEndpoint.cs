using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Categories.ListCategories;

namespace Rpom.Api.Endpoints.Erp.Categories;

internal sealed class ListCategoriesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/categories",
            async (string? search, bool? isActive, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new ListCategories.Query(search, isActive), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Categories")
            .WithName("ListCategories");
    }
}
