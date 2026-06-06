using MediatR;
using Microsoft.AspNetCore.Mvc;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Tables.BatchCreateTables;

namespace Rpom.Api.Endpoints.Erp.Tables;

internal sealed class BatchCreateTablesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/tables/batch",
            async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new BatchCreateTables.Command(
                    request.AreaId, request.CodePrefix, request.StartNumber, request.Count,
                    request.SeatCount, request.Description, request.IsActive), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataManage)
            .WithTags("Tables")
            .WithName("BatchCreateTables");
    }

    internal sealed record Request(
        int AreaId,
        string CodePrefix,
        int StartNumber,
        int Count,
        int SeatCount,
        string? Description,
        bool IsActive);
}
