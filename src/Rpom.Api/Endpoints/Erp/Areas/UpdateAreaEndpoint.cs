using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Areas.UpdateArea;

namespace Rpom.Api.Endpoints.Erp.Areas;

internal sealed class UpdateAreaEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/areas/{id:int}",
            async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new UpdateArea.Command(
                    id, request.CounterId, request.Name, request.Description,
                    request.DisplayOrder, request.IsActive), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Areas")
            .WithName("UpdateArea");
    }

    internal sealed record Request(
        int CounterId, string Name, string? Description, short DisplayOrder, bool IsActive);
}
