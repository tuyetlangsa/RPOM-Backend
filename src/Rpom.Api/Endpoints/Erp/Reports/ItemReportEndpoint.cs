using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Reports;
using Rpom.Application.Reports.ItemReport;

namespace Rpom.Api.Endpoints.Erp.Reports;

internal sealed class ItemReportEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reports/items",
                async (DateTime? fromDate, DateTime? toDate, int? categoryId,
                    int? counterId, int? areaId, ISender sender, CancellationToken ct) =>
                {
                    var filter = new ReportFilter(fromDate, toDate, counterId, areaId, null, categoryId);
                    var result = await sender.Send(new ItemReport.Query(filter), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.ReportItemConsumption)
            .WithTags("Reports").WithName("ItemReport")
            .Produces<ApiResult<IReadOnlyList<ItemReport.Response>>>()
            .WithSummary("Item sales report aggregated by item");
    }
}
