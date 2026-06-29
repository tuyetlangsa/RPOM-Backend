using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Reports;
using Rpom.Application.Reports.ShiftReport;

namespace Rpom.Api.Endpoints.Erp.Reports;

internal sealed class ShiftReportEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reports/shift",
                async (DateTime? fromDate, DateTime? toDate, int? counterId, int? shiftId,
                    ISender sender, CancellationToken ct) =>
                {
                    var filter = new ReportFilter(fromDate, toDate, counterId, null, shiftId, null);
                    var result = await sender.Send(new ShiftReport.Query(filter), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.ReportShift)
            .WithTags("Reports")
            .WithName("ShiftReport")
            .Produces<ApiResult<IReadOnlyList<ShiftReport.Response>>>()
            .WithSummary("Combined shift + variance report");
    }
}
