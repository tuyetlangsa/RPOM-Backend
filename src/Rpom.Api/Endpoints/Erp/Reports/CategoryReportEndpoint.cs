using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Reports;
using Rpom.Application.Reports.CategoryReport;

namespace Rpom.Api.Endpoints.Erp.Reports;

internal sealed class CategoryReportEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reports/categories",
                async (DateTime? fromDate, DateTime? toDate, int? counterId, int? areaId,
                    ISender sender, CancellationToken ct) =>
                {
                    var filter = new ReportFilter(fromDate, toDate, counterId, areaId, null, null);
                    var result = await sender.Send(new CategoryReport.Query(filter), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.ReportItemConsumption)
            .WithTags("Reports").WithName("CategoryReport")
            .Produces<ApiResult<IReadOnlyList<CategoryReport.Response>>>()
            .WithSummary("Revenue by item category");
    }
}
