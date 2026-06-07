using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Tables.GetTable;

namespace Rpom.Api.Endpoints.Erp.Tables;

internal sealed class GetTableEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/tables/{id:int}",
            async (int id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetTable.Query(id), ct);
                return result.MatchOk();
            })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Tables")
            .WithName("GetTable")
            .Produces<ApiResult<GetTable.Response>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a table by id.")
            .WithDescription("Request: route id (int). Response: 200 OK — JSON GetTable.Response.");
    }
}
