using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Reports;
using Rpom.Application.Reports.DetailedRevenueReport;

namespace Rpom.Api.Endpoints.Erp.Reports;

internal sealed class DetailedRevenueReportEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reports/revenue/detail",
                async (DateTime? fromDate, DateTime? toDate, int? counterId,
                    int? areaId, int? shiftId, int pageNumber, int pageSize,
                    ISender sender, CancellationToken ct) =>
                {
                    var filter = new ReportFilter(fromDate, toDate, counterId, areaId, shiftId, null);
                    var query = new DetailedRevenueReport.Query(filter,
                        pageNumber < 1 ? 1 : pageNumber, pageSize < 1 ? 50 : pageSize);
                    var result = await sender.Send(query, ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.ReportRevenue)
            .WithTags("Reports").WithName("DetailedRevenueReport")
            .Produces<ApiResult<DetailedRevenueReport.Response>>()
            .WithSummary("Detailed revenue report — bill list with financial breakdown");
    }
}
