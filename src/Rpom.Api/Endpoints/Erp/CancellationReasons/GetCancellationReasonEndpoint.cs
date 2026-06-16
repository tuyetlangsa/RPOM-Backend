using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.CancellationReasons.GetCancellationReason;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.CancellationReasons;

internal sealed class GetCancellationReasonEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/cancellation-reasons/{id:int}",
                async (int id, ISender sender, CancellationToken ct) =>
                {
                    Result<GetCancellationReason.Response> result = await sender.Send(new GetCancellationReason.Query(id), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("CancellationReasons")
            .WithName("GetCancellationReason")
            .Produces<ApiResult<GetCancellationReason.Response>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a cancellation reason by id.")
            .WithDescription("Request: route id (int). Response: 200 OK — JSON GetCancellationReason.Response.");
    }
}
