using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Tables.UpdateTable;

namespace Rpom.Api.Endpoints.Erp.Tables;

internal sealed class UpdateTableEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/tables/{id:int}",
            async (int id, [FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new UpdateTable.Command(
                    id, request.AreaId, request.Code, request.SeatCount,
                    request.Description, request.IsActive), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Tables")
            .WithName("UpdateTable");
    }

    internal sealed record Request(int AreaId, string Code, int SeatCount, string? Description, bool IsActive);
}
