namespace Rpom.Application.Abstraction.Export;

/// <summary>
///     Export report data to PDF or Excel using Syncfusion.
///     Implementation in Infrastructure layer.
/// </summary>
public interface IReportExportService
{
    /// <summary>Generate a PDF file from a list of rows with headers and title.</summary>
    byte[] GeneratePdf<T>(string title, IReadOnlyList<string> headers, IReadOnlyList<T> rows,
        Func<T, IReadOnlyList<object>> cellSelector);

    /// <summary>Generate an Excel (.xlsx) file from a list of rows with headers and title.</summary>
    byte[] GenerateExcel<T>(string title, IReadOnlyList<string> headers, IReadOnlyList<T> rows,
        Func<T, IReadOnlyList<object>> cellSelector);
}
