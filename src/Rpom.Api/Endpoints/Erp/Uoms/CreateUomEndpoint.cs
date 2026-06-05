using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Uoms.CreateUom;

namespace Rpom.Api.Endpoints.Erp.Uoms;

internal sealed class CreateUomEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/uoms",
            async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateUom.Command(
                    request.Code, request.Name, request.Description, request.IsActive), ct);
                return result.MatchCreated(u => $"/api/uoms/{u.Id}");
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Uoms")
            .WithName("CreateUom");
    }

    internal sealed record Request(string Code, string Name, string? Description, bool IsActive);
}
