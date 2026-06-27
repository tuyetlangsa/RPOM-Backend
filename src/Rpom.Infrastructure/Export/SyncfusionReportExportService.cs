using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;
using Syncfusion.XlsIO;
using Rpom.Application.Abstraction.Export;

namespace Rpom.Infrastructure.Export;

internal sealed class SyncfusionReportExportService : IReportExportService
{
    public byte[] GeneratePdf<T>(string title, IReadOnlyList<string> headers, IReadOnlyList<T> rows,
        Func<T, IReadOnlyList<object>> cellSelector)
    {
        using var document = new PdfDocument();
        var section = document.Sections.Add();
        var page = section.Pages.Add();

        // Title
        var titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 16);
        var titleElement = new PdfTextElement(title, titleFont);
        var layoutResult = titleElement.Draw(page, new Syncfusion.Drawing.PointF(0, 20));
        float y = layoutResult.Bounds.Bottom + 20;

        // Timestamp
        var metaFont = new PdfStandardFont(PdfFontFamily.Helvetica, 9);
        new PdfTextElement($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}", metaFont)
            .Draw(page, new Syncfusion.Drawing.PointF(0, y));
        y += 20;

        // Table
        var grid = new PdfGrid
        {
            DataSource = BuildDataTable(headers, rows, cellSelector)
        };
        grid.Style.Font = new PdfStandardFont(PdfFontFamily.Helvetica, 10);
        grid.Headers[0].Style.Font = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);
        grid.Draw(page, new Syncfusion.Drawing.PointF(0, y));

        using var ms = new MemoryStream();
        document.Save(ms);
        return ms.ToArray();
    }

    public byte[] GenerateExcel<T>(string title, IReadOnlyList<string> headers, IReadOnlyList<T> rows,
        Func<T, IReadOnlyList<object>> cellSelector)
    {
        using var engine = new ExcelEngine();
        var application = engine.Excel;
        var workbook = application.Workbooks.Create(1);
        var worksheet = workbook.Worksheets[0];
        worksheet.Name = title.Length > 31 ? title[..31] : title;

        // Title row
        worksheet.Range[1, 1, 1, headers.Count].Merge();
        worksheet.Range[1, 1].Text = title;
        worksheet.Range[1, 1].CellStyle.Font.Bold = true;
        worksheet.Range[1, 1].CellStyle.Font.Size = 14;

        // Header row
        for (int col = 0; col < headers.Count; col++)
        {
            worksheet.Range[3, col + 1].Text = headers[col];
            worksheet.Range[3, col + 1].CellStyle.Font.Bold = true;
        }

        // Data rows
        for (int rowIdx = 0; rowIdx < rows.Count; rowIdx++)
        {
            var cells = cellSelector(rows[rowIdx]);
            for (int col = 0; col < cells.Count; col++)
            {
                var value = cells[col];
                worksheet.Range[rowIdx + 4, col + 1].Value2 = value switch
                {
                    DateTime dt => dt.ToString("yyyy-MM-dd HH:mm"),
                    _ => value?.ToString() ?? ""
                };
            }
        }

        worksheet.UsedRange.AutofitColumns();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static System.Data.DataTable BuildDataTable<T>(IReadOnlyList<string> headers,
        IReadOnlyList<T> rows, Func<T, IReadOnlyList<object>> cellSelector)
    {
        var dt = new System.Data.DataTable();
        foreach (var h in headers) dt.Columns.Add(h);
        foreach (var row in rows)
        {
            var cells = cellSelector(row);
            dt.Rows.Add(cells.Select(c => c?.ToString() ?? "").ToArray());
        }
        return dt;
    }
}
