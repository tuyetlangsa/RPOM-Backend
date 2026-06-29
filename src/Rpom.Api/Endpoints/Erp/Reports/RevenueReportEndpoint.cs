using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Reports;
using Rpom.Application.Reports.RevenueReport;

namespace Rpom.Api.Endpoints.Erp.Reports;

internal sealed class RevenueReportEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reports/revenue",
                async (DateTime? fromDate, DateTime? toDate, int? counterId,
                    int? areaId, int? shiftId, int? categoryId, string? groupBy,
                    ISender sender, CancellationToken ct) =>
                {
                    var filter = new ReportFilter(fromDate, toDate, counterId, areaId, shiftId, categoryId);
                    var result = await sender.Send(new RevenueReport.Query(filter, groupBy), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.ReportRevenue)
            .WithTags("Reports")
            .WithName("RevenueReport")
            .Produces<ApiResult<RevenueReport.Response>>()
            .WithSummary("Revenue report with 6 metric groups: revenue, volume, discount, payment, operational, comparison");
    }
}
