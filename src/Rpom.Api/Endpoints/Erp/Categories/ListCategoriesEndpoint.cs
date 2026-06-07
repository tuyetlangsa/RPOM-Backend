using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Categories.ListCategories;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Categories;

internal sealed class ListCategoriesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/categories",
                async (string? search, bool? isActive, string? rootCode, ISender sender, CancellationToken ct) =>
                {
                    Result<IReadOnlyList<ListCategories.Response>> result =
                        await sender.Send(new ListCategories.Query(search, isActive, rootCode), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Categories")
            .WithName("ListCategories")
            .Produces<ApiResult<IReadOnlyList<ListCategories.Response>>>()
            .WithSummary("List categories with optional filters.")
            .WithDescription(
                "Request: query search?:string, isActive?:bool, rootCode?:string. Response: 200 OK — JSON array of ListCategories.Response.");
    }
}
