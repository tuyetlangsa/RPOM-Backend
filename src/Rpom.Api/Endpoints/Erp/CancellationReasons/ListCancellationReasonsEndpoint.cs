using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.CancellationReasons.ListCancellationReasons;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.CancellationReasons;

internal sealed class ListCancellationReasonsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/cancellation-reasons",
                async (string? search, bool? isActive, ISender sender, CancellationToken ct) =>
                {
                    Result<IReadOnlyList<ListCancellationReasons.Response>> result =
                        await sender.Send(new ListCancellationReasons.Query(search, isActive), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("CancellationReasons")
            .WithName("ListCancellationReasons")
            .Produces<ApiResult<IReadOnlyList<ListCancellationReasons.Response>>>()
            .WithSummary("List cancellation reasons with optional filters.")
            .WithDescription(
                "Request: query search?:string, isActive?:bool. Response: 200 OK — JSON array of ListCancellationReasons.Response.");
    }
}
