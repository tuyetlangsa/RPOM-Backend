using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Reports;
using Rpom.Application.Reports.TopSellerReport;

namespace Rpom.Api.Endpoints.Erp.Reports;

internal sealed class TopSellerReportEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reports/top-sellers",
                async (DateTime? fromDate, DateTime? toDate, int? counterId, int? areaId,
                    int? topN, string? by, ISender sender, CancellationToken ct) =>
                {
                    var filter = new ReportFilter(fromDate, toDate, counterId, areaId, null, null);
                    var result = await sender.Send(
                        new TopSellerReport.Query(filter, topN ?? 10, by ?? "revenue"), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.ReportItemConsumption)
            .WithTags("Reports").WithName("TopSellerReport")
            .Produces<ApiResult<IReadOnlyList<TopSellerReport.Response>>>()
            .WithSummary("Top selling items by quantity or revenue");
    }
}
