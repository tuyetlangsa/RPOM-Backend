using System.Collections;
using System.Reflection;
using MediatR;
using Microsoft.AspNetCore.Http;
using Rpom.Application.Abstraction.Export;
using Rpom.Application.Access;
using Rpom.Application.Reports;

namespace Rpom.Api.Endpoints.Erp.Reports;

internal sealed class ExportReportEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reports/{reportType}/export",
                async (string reportType, string format,
                    DateTime? fromDate, DateTime? toDate,
                    int? counterId, int? areaId, int? shiftId, int? categoryId,
                    string? groupBy, string? search, bool? lowStock,
                    int? topN, string? by, long? ticketId,
                    ISender sender, IReportExportService exportService,
                    CancellationToken ct) =>
                {
                    var filter = new ReportFilter(fromDate, toDate, counterId, areaId, shiftId, categoryId);
                    bool isPdf = string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);

                    object? data = await FetchReportAsync(reportType, filter, groupBy, search, lowStock, topN, by, ticketId, sender, ct);
                    if (data is null)
                        return Microsoft.AspNetCore.Http.Results.Problem($"Unknown report type: {reportType}", statusCode: StatusCodes.Status400BadRequest);

                    string title = reportType switch
                    {
                        "revenue" => "Revenue Report",
                        "revenue-detail" => "Detailed Revenue Report",
                        "items-detail" => "Item Sales Detail",
                        "shift" => "Shift Report",
                        "categories" => "Category Report",
                        "items" => "Item Report",
                        "top-sellers" => "Top Sellers Report",
                        "ingredient-consumption" => "Ingredient Consumption",
                        "stock-alert" => "Stock Alert",
                        _ => reportType
                    };

                    byte[] bytes = ExportData(data, title, exportService, isPdf);
                    string contentType = isPdf ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    string ext = isPdf ? "pdf" : "xlsx";
                    return Microsoft.AspNetCore.Http.Results.File(bytes, contentType, $"{title.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.{ext}");
                })
            .RequireAuthorization(Permissions.ReportExportExcel)
            .WithTags("Reports").WithName("ExportReport")
            .WithSummary("Export any report as PDF or Excel");
    }

    private static async Task<object?> FetchReportAsync(string reportType, ReportFilter filter,
        string? groupBy, string? search, bool? lowStock, int? topN, string? by, long? ticketId,
        ISender sender, CancellationToken ct)
    {
        return reportType switch
        {
            "revenue" => (await sender.Send(new Application.Reports.RevenueReport.RevenueReport.Query(filter, groupBy ?? "day"), ct)).Value,
            "revenue-detail" => (await sender.Send(new Application.Reports.DetailedRevenueReport.DetailedRevenueReport.Query(filter, 1, 10000), ct)).Value,
            "items-detail" => (await sender.Send(new Application.Reports.ItemSalesDetail.ItemSalesDetail.Query(filter, ticketId, 1, 10000), ct)).Value,
            "shift" => (await sender.Send(new Application.Reports.ShiftReport.ShiftReport.Query(filter), ct)).Value,
            "categories" => (await sender.Send(new Application.Reports.CategoryReport.CategoryReport.Query(filter), ct)).Value,
            "items" => (await sender.Send(new Application.Reports.ItemReport.ItemReport.Query(filter), ct)).Value,
            "top-sellers" => (await sender.Send(new Application.Reports.TopSellerReport.TopSellerReport.Query(filter, topN ?? 10, by ?? "revenue"), ct)).Value,
            "ingredient-consumption" => (await sender.Send(new Application.Reports.IngredientConsumption.IngredientConsumption.Query(filter), ct)).Value,
            "stock-alert" => (await sender.Send(new Application.Reports.StockAlertReport.StockAlertReport.Query(search, lowStock), ct)).Value,
            _ => null
        };
    }

    private static byte[] ExportData(object data, string title, IReportExportService exportService, bool isPdf)
    {
        // Extract list from response object (look for IReadOnlyList or IEnumerable property)
        var rows = ExtractRows(data);
        if (rows.Count == 0)
        {
            // Single object — wrap in list
            rows = new List<object> { data };
        }

        var headers = ExtractHeaders(rows);
        var cellSelector = (object row) => ExtractCells(row, headers);

        return isPdf
            ? exportService.GeneratePdf(title, headers, rows, cellSelector)
            : exportService.GenerateExcel(title, headers, rows, cellSelector);
    }

    private static List<object> ExtractRows(object data)
    {
        // Try to find a collection property on the response
        var type = data.GetType();
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.Name == "Items" || prop.Name == "Bills" || prop.Name == "Breakdown"
                || prop.Name == "Shifts" || prop.Name == "Categories")
            {
                if (prop.GetValue(data) is IEnumerable enumerable)
                    return enumerable.Cast<object>().ToList();
            }
        }

        // Check if data itself is IEnumerable
        if (data is IEnumerable enumerable2 && data is not string)
            return enumerable2.Cast<object>().ToList();

        return new List<object>();
    }

    private static IReadOnlyList<string> ExtractHeaders(List<object> rows)
    {
        if (rows.Count == 0) return new List<string>();
        return rows[0].GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();
    }

    private static IReadOnlyList<object> ExtractCells(object row, IReadOnlyList<string> headers)
    {
        var type = row.GetType();
        return headers.Select(h =>
        {
            var prop = type.GetProperty(h, BindingFlags.Public | BindingFlags.Instance);
            return prop?.GetValue(row) ?? (object)"";
        }).ToList();
    }
}
