using DocumentFormat.OpenXml.Packaging;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using static PanelCladdingEditor.Infrastructure.PanelCladding.OpenXmlPnlSheet;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace PanelCladdingEditor.Infrastructure.PanelCladding;

// Code tables of the PNL sheet in "<PROJECT> - Naming Convention.xlsx". Row 12 holds a title
// cell per table ("Unit-Type", "Wall-Type", "Frame-Config") above its code column; the
// description is the next column and the codes start in row 13. A missing title is added in
// the default column (E, H, J). The tables are reconciled in place: a code keeps its row, a
// placeholder (code without description) is filled, a written description is never changed,
// only the struck state of used/unused rows changes, and new codes go below the table. Every
// other cell, merge and cell type is left as it is.
public sealed class OpenXmlPanelNamingConventionWorkbook
{
    public const string SheetName = OpenXmlPnlSheet.SheetName;
    public const uint TitleRow = OpenXmlPnlSheet.TitleRow;
    public const uint FirstDataRow = OpenXmlPnlSheet.FirstDataRow;
    public const uint HeaderRow = OpenXmlPnlSheet.HeaderRow;
    public const uint ExampleRow = OpenXmlPnlSheet.ExampleRow;
    private const string JobCodeTitle = "Job Code";
    public const string UnchangedMessage = "PANEL_NAMING_CONVENTION_UNCHANGED";

    private enum Table { UnitTypes, WallTypes, FrameConfigs }

    private sealed record TableLayout(Table Table, string Title, string DescriptionTitle, string DefaultColumn);

    private static readonly TableLayout[] Layouts =
    [
        new(Table.UnitTypes, "Unit-Type", "Description", "E"),
        new(Table.WallTypes, "Wall-Type", "Value", "H"),
        new(Table.FrameConfigs, "Frame-Config", "Configuration", "J")
    ];

    private sealed record Placement(TableLayout Layout, string CodeColumn, string DescriptionColumn, S.Cell? Title, S.Cell? DescriptionTitle);

    public OperationResponse<PanelNamingConventionSheetContent> Read(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using SpreadsheetDocument document = SpreadsheetDocument.Open(stream, false);
            OperationResponse<WorksheetPart> sheet = FindSheet(document);
            if (!sheet.Success || sheet.Data?.Worksheet?.GetFirstChild<S.SheetData>() is not S.SheetData sheetData)
            {
                return OperationResponse<PanelNamingConventionSheetContent>.Fail(sheet.Message);
            }
            OperationResponse<Placement[]> placements = Locate(document.WorkbookPart!, sheetData);
            if (!placements.Success || placements.Data is null)
            {
                return OperationResponse<PanelNamingConventionSheetContent>.Fail(placements.Message);
            }
            string recorded = ReadFormatProperty(document);
            OperationResponse<PanelNamingFormat> format = recorded.Length == 0
                ? OperationResponse<PanelNamingFormat>.Ok(PanelNamingFormat.Legacy)
                : PanelNamingFormat.Parse(recorded);
            if (!format.Success || format.Data is null)
            {
                return OperationResponse<PanelNamingConventionSheetContent>.Fail($"{format.Message} in {Path.GetFileName(path)}");
            }
            Dictionary<string, string> Pairs(Table table, Func<string, bool> isCode)
            {
                Placement placement = placements.Data.Single(item => item.Layout.Table == table);
                var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (S.Row row in DataRows(sheetData))
                {
                    string code = CellText(document.WorkbookPart!, row, placement.CodeColumn).Trim();
                    string description = CellText(document.WorkbookPart!, row, placement.DescriptionColumn).Trim();
                    if (isCode(code) && description.Length > 0) pairs.TryAdd(code, description);
                }
                return pairs;
            }
            return OperationResponse<PanelNamingConventionSheetContent>.Ok(new PanelNamingConventionSheetContent
            {
                FrameConfigs = Pairs(Table.FrameConfigs, code => PanelNamingFormat.Fits(format.Data.FrameConfig, code)),
                WallTypes = Pairs(Table.WallTypes, code => code.Length > 0),
                JobCodeWidth = LocateJobCode(document.WorkbookPart!, sheet.Data.Worksheet!, sheetData).Length,
                RecordedFormat = recorded
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                              OpenXmlPackageException or InvalidDataException)
        {
            return OperationResponse<PanelNamingConventionSheetContent>.Fail(
                $"PANEL_NAMING_CONVENTION_READ_FAILED: {Path.GetFileName(path)}: {exception.Message}");
        }
    }

    // Returns Ok(UnchangedMessage) without touching the file when the sheet already shows the tables.
    public OperationResponse Write(string path, PanelNamingConventionTables tables)
    {
        try
        {
            RemoveStaleOwnFiles(path);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (SpreadsheetDocument current = SpreadsheetDocument.Open(stream, false))
            {
                OperationResponse<bool> pending = Reconcile(current, tables, apply: false);
                if (!pending.Success) return pending;
                if (!pending.Data) return OperationResponse.Ok(UnchangedMessage);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                              OpenXmlPackageException or InvalidDataException)
        {
            return OperationResponse.Fail($"PANEL_NAMING_CONVENTION_READ_FAILED: {Path.GetFileName(path)}: {exception.Message}");
        }
        return ReplaceWorkbook(path, document => Reconcile(document, tables, apply: true),
            "Codes are kept in the model and the sheet updates on the next save.", UnchangedMessage);
    }

    // Makes the sheet show the tables, or with apply false only reports whether anything differs.
    private static OperationResponse<bool> Reconcile(SpreadsheetDocument document, PanelNamingConventionTables tables, bool apply)
    {
        OperationResponse<WorksheetPart> sheet = FindSheet(document);
        if (!sheet.Success || sheet.Data?.Worksheet is not S.Worksheet worksheet)
        {
            return OperationResponse<bool>.Fail(sheet.Success ? $"PANEL_NAMING_CONVENTION_SHEET_MISSING: {SheetName}" : sheet.Message);
        }
        WorkbookPart workbookPart = document.WorkbookPart!;
        S.SheetData sheetData = worksheet.GetFirstChild<S.SheetData>() ?? (apply ? worksheet.AppendChild(new S.SheetData()) : new S.SheetData());
        OperationResponse<Placement[]> placements = Locate(workbookPart, sheetData);
        if (!placements.Success || placements.Data is null)
        {
            return OperationResponse<bool>.Fail(placements.Message);
        }
        var styles = new PnlStyleCache(workbookPart.WorkbookStylesPart?.Stylesheet);
        bool changed = false;

        (int Column, string Text)[] jobCode = JobCodeCells(LocateJobCode(workbookPart, worksheet, sheetData), tables.JobCode);
        foreach ((int column, string text) in jobCode)
        {
            S.Row? exampleRow = FindRow(sheetData, ExampleRow);
            if (exampleRow is not null && CellText(workbookPart, exampleRow, ColumnName(column)) == text) continue;
            changed = true;
            if (!apply) continue;
            exampleRow ??= InsertRow(sheetData, ExampleRow);
            uint style = FindCell(exampleRow, ColumnName(column))?.StyleIndex?.Value ?? 0;
            Upsert(exampleRow, InlineCell(ColumnName(column), ExampleRow, text, style));
        }

        foreach (Placement placement in placements.Data)
        {
            if (placement.Title is null)
            {
                changed = true;
                if (apply)
                {
                    S.Row titleRow = FindRow(sheetData, TitleRow) ?? InsertRow(sheetData, TitleRow);
                    Upsert(titleRow, InlineCell(placement.CodeColumn, TitleRow, placement.Layout.Title, 0));
                    if (placement.DescriptionTitle is null)
                    {
                        Upsert(titleRow, InlineCell(placement.DescriptionColumn, TitleRow, placement.Layout.DescriptionTitle, 0));
                    }
                }
            }
            changed |= ReconcileTable(workbookPart, sheetData, styles, placement, Rows(tables, placement.Layout.Table), apply);
        }
        if (apply && changed)
        {
            styles.Save();
            worksheet.GetFirstChild<S.SheetDimension>()?.Remove();
            if (workbookPart.CalculationChainPart is CalculationChainPart calcChain)
            {
                workbookPart.DeletePart(calcChain); // Excel rebuilds it.
            }
            worksheet.Save();
        }
        return OperationResponse<bool>.Ok(changed);
    }

    private static bool ReconcileTable(WorkbookPart workbookPart, S.SheetData sheetData, PnlStyleCache styles, Placement placement,
        IReadOnlyList<PanelNamingCodeRow> items, bool apply)
    {
        string codeColumn = placement.CodeColumn, descriptionColumn = placement.DescriptionColumn;
        var rowByCode = new Dictionary<string, uint>(StringComparer.Ordinal);
        uint last = TitleRow;
        foreach (S.Row row in DataRows(sheetData))
        {
            string code = CellText(workbookPart, row, codeColumn).Trim();
            if (code.Length == 0) continue;
            rowByCode.TryAdd(code, row.RowIndex!.Value);
            last = Math.Max(last, row.RowIndex!.Value);
        }

        // New rows take the table's plain row style, else the title's without bold.
        (uint Code, uint Description)? templates = null;
        (uint Code, uint Description) Templates() => templates ??= (
            Template(workbookPart, sheetData, styles, codeColumn, placement.Title),
            Template(workbookPart, sheetData, styles, descriptionColumn, placement.DescriptionTitle ?? placement.Title));

        bool changed = false;
        foreach (PanelNamingCodeRow item in items)
        {
            bool struck = !item.Used;
            if (!rowByCode.TryGetValue(item.Code, out uint rowIndex))
            {
                // Appended below the table, skipping rows that hold anything in its two columns.
                rowIndex = last + 1;
                while (CellText(workbookPart, FindCell(sheetData, codeColumn, rowIndex)).Trim().Length > 0 ||
                       CellText(workbookPart, FindCell(sheetData, descriptionColumn, rowIndex)).Trim().Length > 0)
                {
                    rowIndex++;
                }
                last = rowIndex;
                changed = true;
                if (!apply) continue;
                S.Row row = FindRow(sheetData, rowIndex) ?? InsertRow(sheetData, rowIndex);
                Upsert(row, ValueCell(codeColumn, rowIndex, item.Code, styles.WithState(Templates().Code, struck)));
                Upsert(row, ValueCell(descriptionColumn, rowIndex, item.Description, styles.WithState(Templates().Description, struck)));
                continue;
            }

            S.Row existing = FindRow(sheetData, rowIndex)!;
            string description = CellText(workbookPart, existing, descriptionColumn).Trim();
            if (description.Length == 0)
            {
                // A placeholder row takes the value; its code cell stays as it is.
                if (item.Description.Length == 0) continue;
                changed = true;
                if (apply)
                {
                    uint style = FindCell(existing, descriptionColumn)?.StyleIndex?.Value ?? Templates().Description;
                    Upsert(existing, ValueCell(descriptionColumn, rowIndex, item.Description, styles.WithState(style, struck)));
                }
            }
            else if (!string.Equals(description, item.Description, StringComparison.Ordinal))
            {
                continue; // A written description is never changed; the sync reports the conflict.
            }
            foreach (S.Cell cell in new[] { FindCell(existing, codeColumn), FindCell(existing, descriptionColumn) }.OfType<S.Cell>())
            {
                uint style = cell.StyleIndex?.Value ?? 0;
                if (styles.IsStruck(style) == struck) continue;
                changed = true;
                if (apply) cell.StyleIndex = styles.WithState(style, struck);
            }
        }
        return changed;
    }

    // The style of the table's first plain row (a struck one maps back to plain), else the
    // title style without bold.
    private static uint Template(WorkbookPart workbookPart, S.SheetData sheetData, PnlStyleCache styles, string column, S.Cell? title)
    {
        S.Cell[] cells = DataRows(sheetData).Select(row => FindCell(row, column))
            .OfType<S.Cell>().Where(cell => CellText(workbookPart, cell).Trim().Length > 0).ToArray();
        S.Cell? template = cells.FirstOrDefault(cell => !styles.IsStruck(cell.StyleIndex?.Value ?? 0)) ?? cells.FirstOrDefault();
        return template is not null ? styles.Plain(template.StyleIndex?.Value ?? 0)
            : title is not null ? styles.Body(title.StyleIndex?.Value ?? 0) : 0;
    }

    // Example-row columns under the row-7 "Job Code" header (its merged span); empty when absent.
    private static int[] LocateJobCode(WorkbookPart workbookPart, S.Worksheet worksheet, S.SheetData sheetData)
    {
        S.Cell? title = FindRow(sheetData, HeaderRow)?.Elements<S.Cell>()
            .FirstOrDefault(cell => Normalize(CellText(workbookPart, cell)) == Normalize(JobCodeTitle));
        if (title is null) return [];
        int column = ColumnIndex(title.CellReference?.Value);
        foreach (S.MergeCell merge in worksheet.GetFirstChild<S.MergeCells>()?.Elements<S.MergeCell>() ?? [])
        {
            string[] ends = (merge.Reference?.Value ?? string.Empty).Split(':');
            if (ends.Length != 2 || RowOf(ends[0]) > HeaderRow || RowOf(ends[1]) < HeaderRow) continue;
            int first = ColumnIndex(ends[0]), last = ColumnIndex(ends[1]);
            if (column >= first && column <= last) return Enumerable.Range(first, last - first + 1).ToArray();
        }
        return [column];
    }

    // One character per cell, or the whole code in a single cell; nothing when it does not fit.
    private static (int Column, string Text)[] JobCodeCells(int[] columns, string jobCode) =>
        !PanelNamingConventionService.JobCodeFits(jobCode, columns.Length) ? []
            : columns.Length == 1 ? [(columns[0], jobCode)]
            : columns.Select((column, index) => (column, jobCode[index].ToString())).ToArray();

    // Title cells in row 12, matched ignoring case, spaces and hyphens; missing ones use defaults.
    private static OperationResponse<Placement[]> Locate(WorkbookPart workbookPart, S.SheetData sheetData)
    {
        S.Row? titleRow = FindRow(sheetData, TitleRow);
        var placements = new List<Placement>();
        foreach (TableLayout layout in Layouts)
        {
            S.Cell[] titles = titleRow?.Elements<S.Cell>()
                .Where(cell => Normalize(CellText(workbookPart, cell)) == Normalize(layout.Title)).ToArray() ?? [];
            if (titles.Length > 1)
            {
                return OperationResponse<Placement[]>.Fail(
                    $"PANEL_NAMING_CONVENTION_LAYOUT_INVALID: row {TitleRow} has more than one '{layout.Title}' title.");
            }
            int codeIndex = titles.Length == 1 ? ColumnIndex(titles[0].CellReference?.Value) : ColumnIndex(layout.DefaultColumn);
            string codeColumn = ColumnName(codeIndex);
            string descriptionColumn = ColumnName(codeIndex + 1);
            S.Cell? descriptionTitle = titleRow is null ? null : FindCell(titleRow, descriptionColumn);
            placements.Add(new Placement(layout, codeColumn, descriptionColumn, titles.SingleOrDefault(),
                descriptionTitle is not null && CellText(workbookPart, descriptionTitle).Trim().Length > 0 ? descriptionTitle : null));
        }
        int[] columns = placements.SelectMany(item => new[] { ColumnIndex(item.CodeColumn), ColumnIndex(item.DescriptionColumn) }).ToArray();
        return columns.Distinct().Count() == columns.Length
            ? OperationResponse<Placement[]>.Ok(placements.ToArray())
            : OperationResponse<Placement[]>.Fail(
                $"PANEL_NAMING_CONVENTION_LAYOUT_INVALID: the Unit-Type, Wall-Type and Frame-Config tables in row {TitleRow} overlap.");
    }

    private static IReadOnlyList<PanelNamingCodeRow> Rows(PanelNamingConventionTables tables, Table table) => table switch
    {
        Table.UnitTypes => tables.UnitTypes,
        Table.WallTypes => tables.WallTypes,
        _ => tables.FrameConfigs
    };
}
