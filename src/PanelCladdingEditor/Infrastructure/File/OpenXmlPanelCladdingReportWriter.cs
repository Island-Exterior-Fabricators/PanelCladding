using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace PanelCladdingEditor.Infrastructure.PanelCladding;

// PCreport export: a "Summary" sheet (model, counts, issues per rule) and an "Issues" sheet with one
// row per issue. Written to a temporary file first so an open workbook is never left half written.
public static class OpenXmlPanelCladdingReportWriter
{
    public const string SummarySheetName = "Summary";
    public const string IssuesSheetName = "Issues";

    public static readonly IReadOnlyList<string> IssueColumns =
        ["Severity", "Category", "Rule", "Object", "Layer", "Detail", "Fix", "Object ID"];

    public static OperationResponse Write(string path, PanelCladdingReport report)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            return OperationResponse.Fail($"PANEL_CLADDING_REPORT_PATH_INVALID: {ex.Message}");
        }
        string tempPath = fullPath + ".writing";
        try
        {
            using (SpreadsheetDocument document = SpreadsheetDocument.Create(tempPath, SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart workbookPart = document.AddWorkbookPart();
                var sheets = new S.Sheets();
                var workbook = new S.Workbook(sheets);
                workbookPart.Workbook = workbook;
                AddStyles(workbookPart);
                AddSheet(workbookPart, sheets, 1, SummarySheetName, [28d, 60d], SummaryRows(report));
                AddSheet(workbookPart, sheets, 2, IssuesSheetName, [10d, 13d, 42d, 26d, 60d, 60d, 60d, 38d], IssueRows(report));
                workbook.Save();
            }
            File.Move(tempPath, fullPath, overwrite: true);
            return OperationResponse.Ok();
        }
        catch (Exception ex)
        {
            try
            {
                File.Delete(tempPath);
            }
            catch (IOException)
            {
            }
            return OperationResponse.Fail($"PANEL_CLADDING_REPORT_EXPORT_FAILED: {ex.Message}");
        }
    }

    private static IEnumerable<IReadOnlyList<string>> SummaryRows(PanelCladdingReport report)
    {
        yield return ["Model", Path.GetFileName(report.DocumentPath)];
        yield return ["Project", report.ProjectCode];
        yield return ["Created", report.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)];
        yield return ["Panels", Number(report.PanelCount)];
        yield return ["Cladding surfaces", Number(report.SurfaceCount)];
        yield return ["Extrusion curves", Number(report.CurveCount)];
        yield return ["Errors", Number(report.Count(PanelCladdingReportSeverity.Error))];
        yield return ["Warnings", Number(report.Count(PanelCladdingReportSeverity.Warning))];
        yield return ["Info", Number(report.Count(PanelCladdingReportSeverity.Info))];
        yield return [string.Empty, string.Empty];
        yield return ["Rule", "Issues"];
        foreach (IGrouping<PanelCladdingReportRule, PanelCladdingReportIssue> group in report.Issues.GroupBy(issue => issue.Rule))
        {
            yield return [$"{group.Key.Severity}: {group.Key.Title}", Number(group.Count())];
        }
    }

    private static IEnumerable<IReadOnlyList<string>> IssueRows(PanelCladdingReport report)
    {
        yield return IssueColumns;
        foreach (PanelCladdingReportIssue issue in report.Issues)
        {
            yield return
            [
                issue.Rule.Severity.ToString(),
                issue.Rule.Category.ToString(),
                issue.Rule.Title,
                issue.ObjectName,
                issue.LayerPath,
                issue.Detail,
                issue.Rule.Fix,
                issue.ObjectId == Guid.Empty ? string.Empty : issue.ObjectId.ToString("D")
            ];
        }
    }

    private static void AddSheet(
        WorkbookPart workbookPart,
        S.Sheets sheets,
        uint sheetId,
        string name,
        IReadOnlyList<double> widths,
        IEnumerable<IReadOnlyList<string>> rows)
    {
        WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var data = new S.SheetData();
        uint rowIndex = 0;
        foreach (IReadOnlyList<string> values in rows)
        {
            rowIndex++;
            var row = new S.Row { RowIndex = rowIndex };
            bool header = rowIndex == 1 || (name == SummarySheetName && values[0] == "Rule");
            for (int column = 0; column < values.Count; column++)
            {
                row.Append(new S.Cell
                {
                    CellReference = ColumnName(column) + rowIndex.ToString(CultureInfo.InvariantCulture),
                    DataType = S.CellValues.InlineString,
                    StyleIndex = header ? 1U : 0U,
                    InlineString = new S.InlineString(new S.Text(values[column] ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve })
                });
            }
            data.Append(row);
        }
        var columns = new S.Columns(widths.Select((width, index) => new S.Column
        {
            Min = (uint)index + 1,
            Max = (uint)index + 1,
            Width = width,
            CustomWidth = true
        }));
        worksheetPart.Worksheet = new S.Worksheet(columns, data);
        sheets.Append(new S.Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = sheetId,
            Name = name
        });
    }

    private static void AddStyles(WorkbookPart workbookPart)
    {
        WorkbookStylesPart styles = workbookPart.AddNewPart<WorkbookStylesPart>();
        var stylesheet = new S.Stylesheet(
            new S.Fonts(new S.Font(), new S.Font(new S.Bold())),
            new S.Fills(
                new S.Fill(new S.PatternFill { PatternType = S.PatternValues.None }),
                new S.Fill(new S.PatternFill { PatternType = S.PatternValues.Gray125 })),
            new S.Borders(new S.Border()),
            new S.CellFormats(
                new S.CellFormat(),
                new S.CellFormat { FontId = 1, ApplyFont = true }));
        styles.Stylesheet = stylesheet;
        stylesheet.Save();
    }

    private static string ColumnName(int index) => ((char)('A' + index)).ToString();

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
