using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Shifts.ListShifts;
using Rpom.Domain.Common;

namespace Rpom.Api.Endpoints.Erp.Shifts;

internal sealed class ListShiftsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/shifts",
                async (string? search, bool? isActive, ISender sender, CancellationToken ct) =>
                {
                    Result<IReadOnlyList<ListShifts.Response>> result =
                        await sender.Send(new ListShifts.Query(search, isActive), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.MasterDataView)
            .WithTags("Shifts")
            .WithName("ListShifts")
            .Produces<ApiResult<IReadOnlyList<ListShifts.Response>>>()
            .WithSummary("List shifts with optional filters.")
            .WithDescription(
                "Request: query search?:string, isActive?:bool. Response: 200 OK — JSON array of ListShifts.Response.");
    }
}
