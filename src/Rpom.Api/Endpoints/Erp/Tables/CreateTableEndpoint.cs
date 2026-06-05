using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Tables.CreateTable;

namespace Rpom.Api.Endpoints.Erp.Tables;

internal sealed class CreateTableEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/tables",
            async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateTable.Command(
                    request.AreaId, request.Code, request.SeatCount,
                    request.Description, request.IsActive), ct);
                return result.MatchCreated(t => $"/api/tables/{t.Id}");
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Tables")
            .WithName("CreateTable");
    }

    internal sealed record Request(int AreaId, string Code, int SeatCount, string? Description, bool IsActive);
}
