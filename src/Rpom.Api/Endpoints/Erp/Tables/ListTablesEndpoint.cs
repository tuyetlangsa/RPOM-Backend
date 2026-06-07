using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Tables.ListTables;

namespace Rpom.Api.Endpoints.Erp.Tables;

internal sealed class ListTablesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/tables",
            async (int? counterId, int? areaId, string? search, bool? isActive,
                   ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new ListTables.Query(counterId, areaId, search, isActive), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Tables")
            .WithName("ListTables")
            .Produces<ApiResult<IReadOnlyList<ListTables.Response>>>(StatusCodes.Status200OK)
            .WithSummary("List tables with optional filters.")
            .WithDescription("Request: query counterId?:int, areaId?:int, search?:string, isActive?:bool. Response: 200 OK — JSON array of ListTables.Response.");
    }
}
