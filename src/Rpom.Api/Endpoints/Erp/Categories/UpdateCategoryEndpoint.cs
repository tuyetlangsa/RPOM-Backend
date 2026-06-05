using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Categories.UpdateCategory;

namespace Rpom.Api.Endpoints.Erp.Categories;

internal sealed class UpdateCategoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/categories/{id:int}",
            async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new UpdateCategory.Command(
                    id, request.Code, request.Name, request.Description, request.ParentId,
                    request.DisplayOrder, request.IsActive), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Categories")
            .WithName("UpdateCategory");
    }

    internal sealed record Request(
        string Code, string Name, string? Description, int? ParentId,
        short DisplayOrder, bool IsActive);
}
