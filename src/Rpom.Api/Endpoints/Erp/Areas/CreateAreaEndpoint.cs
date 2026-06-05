using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Areas.CreateArea;

namespace Rpom.Api.Endpoints.Erp.Areas;

internal sealed class CreateAreaEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/areas",
            async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateArea.Command(
                    request.CounterId, request.Name, request.Description,
                    request.DisplayOrder, request.IsActive), ct);
                return result.MatchCreated(a => $"/api/areas/{a.Id}");
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Areas")
            .WithName("CreateArea");
    }

    internal sealed record Request(
        int CounterId, string Name, string? Description, short DisplayOrder, bool IsActive);
}
