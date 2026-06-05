using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Categories.CreateCategory;

namespace Rpom.Api.Endpoints.Erp.Categories;

internal sealed class CreateCategoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/categories",
            async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateCategory.Command(
                    request.Code, request.Name, request.Description, request.ParentId,
                    request.DisplayOrder, request.IsActive), ct);
                return result.MatchCreated(c => $"/api/categories/{c.Id}");
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Categories")
            .WithName("CreateCategory");
    }

    internal sealed record Request(
        string Code, string Name, string? Description, int? ParentId,
        short DisplayOrder, bool IsActive);
}
