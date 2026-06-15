using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Lookups.GetCancellationReasons;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.System.Lookups;

internal sealed class GetCancellationReasonsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/lookups/cancellation-reasons", async (ISender sender, CancellationToken ct) =>
            {
                Result<IReadOnlyList<GetCancellationReasons.CancellationReasonItem>> result =
                    await sender.Send(new GetCancellationReasons.Query(), ct);
                return result.MatchOk();
            })
            .RequireAuthorization()
            .WithTags("Lookups")
            .WithName("GetCancellationReasons")
            .Produces<ApiResult<IReadOnlyList<GetCancellationReasons.CancellationReasonItem>>>()
            .WithSummary("List active cancellation reasons for refund/cancel selection lists.")
            .WithDescription("Request: none. Response: 200 OK — JSON array of CancellationReasonItem.");
    }
}
