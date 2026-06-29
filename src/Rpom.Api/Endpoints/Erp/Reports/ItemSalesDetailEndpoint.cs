using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Reports;
using Rpom.Application.Reports.ItemSalesDetail;

namespace Rpom.Api.Endpoints.Erp.Reports;

internal sealed class ItemSalesDetailEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reports/items/detail",
                async (DateTime? fromDate, DateTime? toDate, int? categoryId,
                    int? counterId, int? areaId, long? ticketId,
                    int pageNumber, int pageSize,
                    ISender sender, CancellationToken ct) =>
                {
                    var filter = new ReportFilter(fromDate, toDate, counterId, areaId, null, categoryId);
                    var query = new ItemSalesDetail.Query(filter, ticketId,
                        pageNumber < 1 ? 1 : pageNumber, pageSize < 1 ? 20 : pageSize);
                    var result = await sender.Send(query, ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.ReportItemConsumption)
            .WithTags("Reports").WithName("ItemSalesDetail")
            .Produces<ApiResult<ItemSalesDetail.Response>>()
            .WithSummary("Item sales detail — bills with items nested, drill-down from revenue report");
    }
}
