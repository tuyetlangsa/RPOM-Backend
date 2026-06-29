using MediatR;
using Rpom.Api.Results;
using Rpom.Application.Access;
using Rpom.Application.Reports.StockAlertReport;

namespace Rpom.Api.Endpoints.Erp.Reports;

internal sealed class StockAlertReportEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reports/stock-alert",
                async (string? search, bool? lowStock, ISender sender, CancellationToken ct) =>
                {
                    var result = await sender.Send(new StockAlertReport.Query(search, lowStock), ct);
                    return result.MatchOk();
                })
            .RequireAuthorization(Permissions.ReportItemConsumption)
            .WithTags("Reports").WithName("StockAlertReport")
            .Produces<ApiResult<IReadOnlyList<StockAlertReport.Response>>>()
            .WithSummary("Stock level alert — items below low-stock threshold");
    }
}
