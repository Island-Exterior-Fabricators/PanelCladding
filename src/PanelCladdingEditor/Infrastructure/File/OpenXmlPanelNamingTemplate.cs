using DocumentFormat.OpenXml.Packaging;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using static PanelCladdingEditor.Infrastructure.PanelCladding.OpenXmlPnlSheet;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace PanelCladdingEditor.Infrastructure.PanelCladding;

// Configures the PNL sheet for a project's PNL format (PCtemplatePNL). Row 7 holds the segment
// headers (Wall-Type, Frame-Config, Frame-Type, Cladding, Definition; a header spans its merged
// columns). Wall-Type is as wide as its code; the other segments are at least two columns, the
// code and description columns of the table beneath them. Row 8 shows the code, one character
// per cell, merged across the segment for a one-character code. Each segment's table (row-12
// title) keeps rows 13-36 filled with placeholder codes. The format is recorded in the custom
// property PanelCladdingEditor.PNLFormat. Codes that have a value are never changed or moved.
public sealed class OpenXmlPanelNamingTemplate
{
    public const string UnchangedMessage = "PANEL_NAMING_TEMPLATE_UNCHANGED";

    private sealed record Segment(string Title, int First, int Last)
    {
        public int Width => Last - First + 1;
    }

    // Tables of the sync keep its default columns; the others default to their header column.
    private static readonly IReadOnlyDictionary<string, string> SyncTableColumns =
        new Dictionary<string, string>(StringComparer.Ordinal) { ["Wall-Type"] = "H", ["Frame-Config"] = "J" };

    public OperationResponse<PanelNamingTemplateState> Inspect(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using SpreadsheetDocument document = SpreadsheetDocument.Open(stream, false);
            OperationResponse<Sheet> sheet = Open(document);
            if (!sheet.Success || sheet.Data is null) return OperationResponse<PanelNamingTemplateState>.Fail(sheet.Message);
            OperationResponse<Segment[]> segments = LocateSegments(sheet.Data);
            if (!segments.Success || segments.Data is null) return OperationResponse<PanelNamingTemplateState>.Fail(segments.Message);
            OperationResponse<Dictionary<string, int>> tables = LocateTables(sheet.Data, segments.Data);
            if (!tables.Success || tables.Data is null) return OperationResponse<PanelNamingTemplateState>.Fail(tables.Message);
            return OperationResponse<PanelNamingTemplateState>.Ok(new PanelNamingTemplateState
            {
                RecordedFormat = ReadFormatProperty(document),
                WrittenCodes = tables.Data.ToDictionary(item => item.Key,
                    item => (IReadOnlyList<string>)WrittenCodes(sheet.Data, item.Value), StringComparer.Ordinal)
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                              OpenXmlPackageException or InvalidDataException)
        {
            return OperationResponse<PanelNamingTemplateState>.Fail(
                $"PANEL_NAMING_CONVENTION_READ_FAILED: {Path.GetFileName(path)}: {exception.Message}");
        }
    }

    // Codes in the given segments' tables that have a value and do not fit the format.
    public static IReadOnlyList<string> Misfits(PanelNamingTemplateState state, string title, string token) =>
        state.WrittenCodes.TryGetValue(title, out IReadOnlyList<string>? codes)
            ? codes.Where(code => !PanelNamingFormat.Fits(token, code)).ToArray()
            : [];

    // Returns Ok(result) with Changed false and the file untouched when the sheet is already configured.
    public OperationResponse<PanelNamingTemplateResult> Apply(string path, PanelNamingFormat format)
    {
        RemoveStaleOwnFiles(path);
        var notes = new List<string>();
        OperationResponse written = ReplaceWorkbook(path, document => Configure(document, format, notes),
            "Close it in Excel and run PCtemplatePNL again.", UnchangedMessage);
        return written.Success
            ? OperationResponse<PanelNamingTemplateResult>.Ok(new PanelNamingTemplateResult
            {
                Changed = written.Message != UnchangedMessage, Notes = notes
            })
            : OperationResponse<PanelNamingTemplateResult>.Fail(written.Message);
    }

    private sealed record Sheet(WorkbookPart WorkbookPart, WorksheetPart WorksheetPart, S.Worksheet Worksheet, S.SheetData SheetData)
    {
        public string Text(string column, uint row) => CellText(WorkbookPart, FindCell(SheetData, column, row));
    }

    private static OperationResponse<Sheet> Open(SpreadsheetDocument document)
    {
        OperationResponse<WorksheetPart> part = FindSheet(document);
        if (!part.Success || part.Data?.Worksheet is not S.Worksheet worksheet)
        {
            return OperationResponse<Sheet>.Fail(part.Success ? $"PANEL_NAMING_CONVENTION_SHEET_MISSING: {SheetName}" : part.Message);
        }
        S.SheetData sheetData = worksheet.GetFirstChild<S.SheetData>() ?? worksheet.AppendChild(new S.SheetData());
        return OperationResponse<Sheet>.Ok(new Sheet(document.WorkbookPart!, part.Data, worksheet, sheetData));
    }

    private static OperationResponse<bool> Configure(SpreadsheetDocument document, PanelNamingFormat format, List<string> notes)
    {
        OperationResponse<Sheet> opened = Open(document);
        if (!opened.Success || opened.Data is null) return OperationResponse<bool>.Fail(opened.Message);
        Sheet sheet = opened.Data;
        OperationResponse<Segment[]> located = LocateSegments(sheet);
        if (!located.Success || located.Data is null) return OperationResponse<bool>.Fail(located.Message);
        OperationResponse<Dictionary<string, int>> tables = LocateTables(sheet, located.Data);
        if (!tables.Success || tables.Data is null) return OperationResponse<bool>.Fail(tables.Message);

        // Written codes must fit (codes never move); Definition is left alone when they do not.
        var tokens = format.Segments.ToDictionary(item => item.Title, item => item.Token, StringComparer.Ordinal);
        var configured = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string title, string token) in format.Segments)
        {
            string[] misfits = WrittenCodes(sheet, tables.Data[title]).Where(code => !PanelNamingFormat.Fits(token, code)).ToArray();
            if (misfits.Length == 0)
            {
                configured.Add(title);
                continue;
            }
            string message = $"{title} codes {string.Join(", ", misfits)} have values and do not fit {token}";
            if (title != PanelNamingFormat.SegmentTitles[4])
            {
                return OperationResponse<bool>.Fail($"PANEL_NAMING_TEMPLATE_CODES_DO_NOT_FIT: {message}; codes never move.");
            }
            notes.Add($"Definition left as it is: {message}.");
        }

        bool changed = false;
        Segment[] segments = located.Data.ToArray();
        int[] widths = segments.Select(segment => !configured.Contains(segment.Title) ? segment.Width
            : segment.Title == PanelNamingFormat.SegmentTitles[0] ? tokens[segment.Title].Length
            : Math.Max(2, tokens[segment.Title].Length)).ToArray();
        if (segments.Select((segment, index) => segment.Width != widths[index]).Any(differs => differs))
        {
            OperationResponse supported = CheckShiftSupported(document, sheet);
            if (!supported.Success) return OperationResponse<bool>.Fail(supported.Message);
            for (int index = segments.Length - 1; index >= 0; index--)
            {
                int delta = widths[index] - segments[index].Width;
                if (delta == 0) continue;
                OperationResponse shifted = delta > 0
                    ? InsertColumns(sheet, segments[index].Last + 1, delta)
                    : DeleteColumns(sheet, segments[index], segments[index].Last + delta + 1, -delta);
                if (!shifted.Success) return OperationResponse<bool>.Fail(shifted.Message);
                notes.Add($"{segments[index].Title} now spans {widths[index]} column(s).");
                foreach (string title in tables.Data.Keys.ToArray())
                {
                    if (tables.Data[title] > segments[index].Last) tables.Data[title] += delta;
                }
            }
            sheet.Worksheet.GetFirstChild<S.SheetDimension>()?.Remove();
            changed = true;
            int offset = 0;
            for (int index = 0; index < segments.Length; index++)
            {
                segments[index] = segments[index] with { First = segments[index].First + offset, Last = segments[index].First + offset + widths[index] - 1 };
                offset += widths[index] - located.Data[index].Width;
            }
        }

        var styles = new PnlStyleCache(sheet.WorkbookPart.WorkbookStylesPart?.Stylesheet);
        foreach (Segment segment in segments.Where(segment => configured.Contains(segment.Title)))
        {
            string token = tokens[segment.Title];
            changed |= MergeRow(sheet, HeaderRow, segment.First, segment.Last);
            changed |= WriteExample(sheet, segment, token);
            changed |= FillPlaceholders(sheet, styles, segment, tables.Data[segment.Title], token, notes);
        }
        if (ReadFormatProperty(document) != format.Serialize())
        {
            WriteFormatProperty(document, format.Serialize());
            changed = true;
        }
        if (changed)
        {
            styles.Save();
            sheet.Worksheet.Save();
        }
        return OperationResponse<bool>.Ok(changed);
    }

    // Row-7 headers in segment order, each spanning its merge.
    private static OperationResponse<Segment[]> LocateSegments(Sheet sheet)
    {
        S.Row? header = FindRow(sheet.SheetData, HeaderRow);
        var segments = new List<Segment>();
        foreach (string title in PanelNamingFormat.SegmentTitles)
        {
            S.Cell[] cells = header?.Elements<S.Cell>()
                .Where(cell => Normalize(CellText(sheet.WorkbookPart, cell)) == Normalize(title)).ToArray() ?? [];
            if (cells.Length != 1)
            {
                return OperationResponse<Segment[]>.Fail(
                    $"PANEL_NAMING_TEMPLATE_LAYOUT_INVALID: row {HeaderRow} needs exactly one '{title}' header.");
            }
            int column = ColumnIndex(cells[0].CellReference?.Value);
            (int first, int last) = (column, column);
            foreach ((int c1, uint r1, int c2, uint r2) in Merges(sheet.Worksheet))
            {
                if (r1 <= HeaderRow && r2 >= HeaderRow && column >= c1 && column <= c2) (first, last) = (c1, c2);
            }
            segments.Add(new Segment(title, first, last));
        }
        for (int index = 1; index < segments.Count; index++)
        {
            if (segments[index].First <= segments[index - 1].Last)
            {
                return OperationResponse<Segment[]>.Fail(
                    $"PANEL_NAMING_TEMPLATE_LAYOUT_INVALID: the row {HeaderRow} headers must be in the order {string.Join(", ", PanelNamingFormat.SegmentTitles)}.");
            }
        }
        return OperationResponse<Segment[]>.Ok(segments.ToArray());
    }

    // The code column of each segment's table: its row-12 title, else the default column.
    private static OperationResponse<Dictionary<string, int>> LocateTables(Sheet sheet, Segment[] segments)
    {
        S.Row? titleRow = FindRow(sheet.SheetData, TitleRow);
        var columns = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Segment segment in segments)
        {
            S.Cell[] titles = titleRow?.Elements<S.Cell>()
                .Where(cell => Normalize(CellText(sheet.WorkbookPart, cell)) == Normalize(segment.Title)).ToArray() ?? [];
            if (titles.Length > 1)
            {
                return OperationResponse<Dictionary<string, int>>.Fail(
                    $"PANEL_NAMING_CONVENTION_LAYOUT_INVALID: row {TitleRow} has more than one '{segment.Title}' title.");
            }
            columns[segment.Title] = titles.Length == 1 ? ColumnIndex(titles[0].CellReference?.Value)
                : SyncTableColumns.TryGetValue(segment.Title, out string? column) ? ColumnIndex(column) : segment.First;
        }
        int unitType = titleRow?.Elements<S.Cell>().Where(cell => Normalize(CellText(sheet.WorkbookPart, cell)) == "UNITTYPE")
            .Select(cell => ColumnIndex(cell.CellReference?.Value)).FirstOrDefault() is int found and > 0 ? found : ColumnIndex("E");
        int[] used = columns.Values.Append(unitType).SelectMany(column => new[] { column, column + 1 }).ToArray();
        return used.Distinct().Count() == used.Length
            ? OperationResponse<Dictionary<string, int>>.Ok(columns)
            : OperationResponse<Dictionary<string, int>>.Fail(
                $"PANEL_NAMING_CONVENTION_LAYOUT_INVALID: the row {TitleRow} tables overlap (each needs a code and a description column).");
    }

    private static string[] WrittenCodes(Sheet sheet, int codeColumn) =>
        DataRows(sheet.SheetData)
            .Select(row => (Code: CellText(sheet.WorkbookPart, row, ColumnName(codeColumn)).Trim(),
                Description: CellText(sheet.WorkbookPart, row, ColumnName(codeColumn + 1)).Trim()))
            .Where(item => item.Code.Length > 0 && item.Description.Length > 0)
            .Select(item => item.Code).Distinct(StringComparer.Ordinal).ToArray();

    private static bool WriteExample(Sheet sheet, Segment segment, string token)
    {
        bool changed = MergeRow(sheet, ExampleRow, segment.First, segment.Last, merged: token.Length == 1);
        S.Row row = FindRow(sheet.SheetData, ExampleRow) ?? InsertRow(sheet.SheetData, ExampleRow);
        uint fallback = FindCell(row, ColumnName(segment.First))?.StyleIndex?.Value ?? 0;
        for (int column = segment.First; column <= segment.Last; column++)
        {
            int index = column - segment.First;
            string text = token.Length == 1 && index > 0 ? string.Empty : token[index].ToString();
            S.Cell? cell = FindCell(row, ColumnName(column));
            if (CellText(sheet.WorkbookPart, cell) == text) continue;
            uint style = cell?.StyleIndex?.Value ?? fallback;
            Upsert(row, text.Length == 0 ? new S.Cell { CellReference = ColumnName(column) + ExampleRow, StyleIndex = style }
                : ValueCell(ColumnName(column), ExampleRow, text, style));
            changed = true;
        }
        return changed;
    }

    // Rows 13-36 under the table: rows with neither code nor value get the next free codes; a code
    // without a value that does not fit the format is renumbered. Grouped frame-config codes (AA,
    // A1) take their first letter from the configuration, so they get no placeholders.
    private static bool FillPlaceholders(Sheet sheet, PnlStyleCache styles, Segment segment, int codeColumn, string token, List<string> notes)
    {
        if (segment.Title == PanelNamingFormat.SegmentTitles[1] && PanelNamingFormat.IsGrouped(token)) return false;
        string code = ColumnName(codeColumn), description = ColumnName(codeColumn + 1);
        bool changed = EnsureTitle(sheet, segment.Title, codeColumn);
        var present = new HashSet<string>(StringComparer.Ordinal);
        var refit = new List<uint>();
        foreach (S.Row row in DataRows(sheet.SheetData))
        {
            string text = CellText(sheet.WorkbookPart, row, code).Trim();
            if (text.Length == 0) continue;
            if (CellText(sheet.WorkbookPart, row, description).Trim().Length == 0 && !PanelNamingFormat.Fits(token, text)) refit.Add(row.RowIndex!.Value);
            else present.Add(text);
        }
        S.Cell? sample = DataRows(sheet.SheetData).Select(row => FindCell(row, code))
            .FirstOrDefault(cell => cell is not null && CellText(sheet.WorkbookPart, cell).Trim().Length > 0 && !refit.Contains(RowOf(cell.CellReference!.Value!)));
        S.Cell? title = FindCell(sheet.SheetData, code, TitleRow);
        uint template = sample is not null ? styles.Plain(sample.StyleIndex?.Value ?? 0) : styles.Body(title?.StyleIndex?.Value ?? 0);
        using IEnumerator<string> free = PanelNamingFormat.Sequence(token).Where(candidate => !present.Contains(candidate)).GetEnumerator();
        int filled = 0;
        for (uint rowIndex = FirstDataRow; rowIndex < FirstDataRow + PanelNamingFormat.PlaceholderCount; rowIndex++)
        {
            bool empty = refit.Contains(rowIndex) || sheet.Text(code, rowIndex).Trim().Length == 0 && sheet.Text(description, rowIndex).Trim().Length == 0;
            if (!empty || !free.MoveNext()) continue;
            S.Row row = FindRow(sheet.SheetData, rowIndex) ?? InsertRow(sheet.SheetData, rowIndex);
            uint style = FindCell(row, code)?.StyleIndex?.Value is uint existing && existing != 0 ? styles.Plain(existing) : template;
            Upsert(row, ValueCell(code, rowIndex, free.Current, style));
            refit.Remove(rowIndex);
            filled++;
        }
        foreach (uint rowIndex in refit)
        {
            S.Cell stale = FindCell(sheet.SheetData, code, rowIndex)!;
            Upsert(FindRow(sheet.SheetData, rowIndex)!, new S.Cell { CellReference = stale.CellReference?.Value, StyleIndex = stale.StyleIndex?.Value });
            filled++;
        }
        if (filled > 0) notes.Add($"{segment.Title}: {filled} placeholder row(s) set in column {code}.");
        return changed || filled > 0;
    }

    // Adds a missing row-12 title in the style of another table title.
    private static bool EnsureTitle(Sheet sheet, string title, int codeColumn)
    {
        string column = ColumnName(codeColumn);
        if (Normalize(sheet.Text(column, TitleRow)) == Normalize(title)) return false;
        S.Row titleRow = FindRow(sheet.SheetData, TitleRow) ?? InsertRow(sheet.SheetData, TitleRow);
        uint style = PanelNamingFormat.SegmentTitles.Append("Unit-Type")
            .Select(other => titleRow.Elements<S.Cell>().FirstOrDefault(cell => Normalize(CellText(sheet.WorkbookPart, cell)) == Normalize(other)))
            .FirstOrDefault(cell => cell is not null)?.StyleIndex?.Value ?? 0;
        Upsert(titleRow, InlineCell(column, TitleRow, title, style));
        return true;
    }

    // Makes the row's cells first..last one merge (merged and wider than a cell) or unmerged.
    // Other merges that lie only in this row and overlap the range are removed.
    private static bool MergeRow(Sheet sheet, uint row, int first, int last, bool merged = true)
    {
        S.MergeCells? merges = sheet.Worksheet.GetFirstChild<S.MergeCells>();
        string wanted = $"{ColumnName(first)}{row}:{ColumnName(last)}{row}";
        merged &= last > first;
        bool changed = false;
        bool present = false;
        foreach (S.MergeCell merge in merges?.Elements<S.MergeCell>().ToArray() ?? [])
        {
            (int c1, uint r1, int c2, uint r2) = ParseRange(merge.Reference?.Value);
            if (r1 != row || r2 != row || c2 < first || c1 > last) continue;
            if (merged && c1 == first && c2 == last)
            {
                present = true;
                continue;
            }
            merge.Remove();
            changed = true;
        }
        if (merged && !present)
        {
            merges ??= InsertMergeCells(sheet.Worksheet);
            merges.AppendChild(new S.MergeCell { Reference = wanted });
            changed = true;
        }
        UpdateMergeCount(sheet.Worksheet);
        return changed;
    }

    private static S.MergeCells InsertMergeCells(S.Worksheet worksheet)
    {
        var merges = new S.MergeCells();
        // mergeCells follows sheetData (and phonetic/conditional formatting elements, absent here).
        return worksheet.InsertAfter(merges, worksheet.GetFirstChild<S.SheetData>()!);
    }

    private static void UpdateMergeCount(S.Worksheet worksheet)
    {
        if (worksheet.GetFirstChild<S.MergeCells>() is not S.MergeCells merges) return;
        int count = merges.Elements<S.MergeCell>().Count();
        if (count == 0) merges.Remove();
        else merges.Count = (uint)count;
    }

    private static IEnumerable<(int C1, uint R1, int C2, uint R2)> Merges(S.Worksheet worksheet) =>
        (worksheet.GetFirstChild<S.MergeCells>()?.Elements<S.MergeCell>() ?? []).Select(merge => ParseRange(merge.Reference?.Value));

    private static (int C1, uint R1, int C2, uint R2) ParseRange(string? range)
    {
        string[] ends = (range ?? string.Empty).Split(':');
        string last = ends.Length > 1 ? ends[1] : ends[0];
        return (ColumnIndex(ends[0]), RowOf(ends[0]), ColumnIndex(last), RowOf(last));
    }

    // Column shifts rewrite cell references, merges and column widths only, so sheets with
    // anything else that refers to cells are left for the user to adjust.
    private static OperationResponse CheckShiftSupported(SpreadsheetDocument document, Sheet sheet)
    {
        var found = new List<string>();
        if (sheet.Worksheet.Descendants<S.CellFormula>().Any()) found.Add("formulas");
        if (sheet.Worksheet.Elements<S.ConditionalFormatting>().Any()) found.Add("conditional formatting");
        if (sheet.Worksheet.Elements<S.DataValidations>().Any()) found.Add("data validation");
        if (sheet.Worksheet.Elements<S.Hyperlinks>().Any()) found.Add("hyperlinks");
        if (sheet.Worksheet.Elements<S.AutoFilter>().Any()) found.Add("filters");
        if (sheet.WorksheetPart.TableDefinitionParts.Any()) found.Add("tables");
        if (sheet.WorksheetPart.DrawingsPart is not null || sheet.WorksheetPart.WorksheetCommentsPart is not null ||
            sheet.Worksheet.Elements<S.LegacyDrawing>().Any()) found.Add("drawings or comments");
        if (document.WorkbookPart?.Workbook?.DefinedNames?.Elements<S.DefinedName>()
                .Any(name => name.Text.Contains(SheetName + "!", StringComparison.OrdinalIgnoreCase)) == true) found.Add("defined names");
        return found.Count == 0 ? OperationResponse.Ok()
            : OperationResponse.Fail($"PANEL_NAMING_TEMPLATE_SHIFT_UNSUPPORTED: the {SheetName} sheet has {string.Join(", ", found)}; " +
                "adjust the segment columns in Excel and run PCtemplatePNL again.");
    }

    // Inserts count columns at index `at` (the column after a segment). Cells and merges from `at`
    // move right; merges reaching the segment's last column grow with it; header rows (1-11) get
    // the style of that column in the new cells; the new columns take its width.
    private static OperationResponse InsertColumns(Sheet sheet, int at, int count)
    {
        foreach (S.Row row in sheet.SheetData.Elements<S.Row>())
        {
            row.Spans = null;
            uint rowIndex = row.RowIndex!.Value;
            foreach (S.Cell cell in row.Elements<S.Cell>().Where(cell => ColumnIndex(cell.CellReference?.Value) >= at).ToArray())
            {
                cell.CellReference = ColumnName(ColumnIndex(cell.CellReference?.Value) + count) + rowIndex;
            }
            if (rowIndex < TitleRow && FindCell(row, ColumnName(at - 1))?.StyleIndex?.Value is uint style)
            {
                for (int column = at; column < at + count; column++)
                {
                    Upsert(row, new S.Cell { CellReference = ColumnName(column) + rowIndex, StyleIndex = style });
                }
            }
        }
        foreach (S.MergeCell merge in sheet.Worksheet.GetFirstChild<S.MergeCells>()?.Elements<S.MergeCell>() ?? [])
        {
            (int c1, uint r1, int c2, uint r2) = ParseRange(merge.Reference?.Value);
            if (c1 >= at) (c1, c2) = (c1 + count, c2 + count);
            else if (c2 >= at - 1) c2 += count;
            merge.Reference = $"{ColumnName(c1)}{r1}:{ColumnName(c2)}{r2}";
        }
        ShiftColumnDefinitions(sheet.Worksheet, at, count);
        return OperationResponse.Ok();
    }

    // Removes count columns from `at` (the end of a segment). Only rows 7 and 8 (header span and
    // example, both rewritten) may hold anything there.
    private static OperationResponse DeleteColumns(Sheet sheet, Segment segment, int at, int count)
    {
        foreach (S.Row row in sheet.SheetData.Elements<S.Row>().Where(row => row.RowIndex?.Value is not (HeaderRow or ExampleRow)))
        {
            S.Cell? filled = row.Elements<S.Cell>().FirstOrDefault(cell =>
                ColumnIndex(cell.CellReference?.Value) is int column && column >= at && column < at + count &&
                CellText(sheet.WorkbookPart, cell).Trim().Length > 0);
            if (filled is not null)
            {
                return OperationResponse.Fail($"PANEL_NAMING_TEMPLATE_COLUMNS_NOT_EMPTY: {segment.Title} needs fewer columns but " +
                    $"{filled.CellReference?.Value} holds '{CellText(sheet.WorkbookPart, filled)}'; clear it or adjust the columns in Excel.");
            }
        }
        foreach (S.Row row in sheet.SheetData.Elements<S.Row>())
        {
            row.Spans = null;
            uint rowIndex = row.RowIndex!.Value;
            foreach (S.Cell cell in row.Elements<S.Cell>().ToArray())
            {
                int column = ColumnIndex(cell.CellReference?.Value);
                if (column >= at && column < at + count) cell.Remove();
                else if (column >= at + count) cell.CellReference = ColumnName(column - count) + rowIndex;
            }
        }
        S.MergeCells? merges = sheet.Worksheet.GetFirstChild<S.MergeCells>();
        foreach (S.MergeCell merge in merges?.Elements<S.MergeCell>().ToArray() ?? [])
        {
            (int c1, uint r1, int c2, uint r2) = ParseRange(merge.Reference?.Value);
            int Shift(int column, bool end) => column >= at + count ? column - count : column >= at ? (end ? at - 1 : at) : column;
            (c1, c2) = (Shift(c1, end: false), Shift(c2, end: true));
            if (c2 < c1 || (c1 == c2 && r1 == r2)) merge.Remove();
            else merge.Reference = $"{ColumnName(c1)}{r1}:{ColumnName(c2)}{r2}";
        }
        UpdateMergeCount(sheet.Worksheet);
        ShiftColumnDefinitions(sheet.Worksheet, at, -count);
        return OperationResponse.Ok();
    }

    // Column widths: definitions from `at` move by count; inserted columns copy column at - 1.
    private static void ShiftColumnDefinitions(S.Worksheet worksheet, int at, int count)
    {
        if (worksheet.GetFirstChild<S.Columns>() is not S.Columns columns) return;
        const int maxColumn = 16384;
        var byIndex = new SortedDictionary<int, S.Column>();
        foreach (S.Column column in columns.Elements<S.Column>())
        {
            for (int index = (int)(column.Min?.Value ?? 0); index <= (int)(column.Max?.Value ?? 0) && index <= maxColumn; index++)
            {
                byIndex[index] = column;
            }
        }
        var shifted = new SortedDictionary<int, S.Column>();
        foreach ((int index, S.Column column) in byIndex)
        {
            int target = index < at ? index : count < 0 && index < at - count ? 0 : index + count;
            if (target is > 0 and <= maxColumn) shifted[target] = column;
        }
        if (count > 0 && byIndex.TryGetValue(at - 1, out S.Column? source))
        {
            for (int index = at; index < at + count; index++) shifted[index] = source;
        }
        var rebuilt = new List<S.Column>();
        foreach ((int index, S.Column column) in shifted)
        {
            S.Column? previous = rebuilt.LastOrDefault();
            if (previous is not null && previous.Max!.Value + 1 == index && SameDefinition(previous, column))
            {
                previous.Max = (uint)index;
                continue;
            }
            var copy = (S.Column)column.CloneNode(true);
            copy.Min = (uint)index;
            copy.Max = (uint)index;
            rebuilt.Add(copy);
        }
        columns.RemoveAllChildren();
        foreach (S.Column column in rebuilt) columns.AppendChild(column);
        if (!columns.HasChildren) columns.Remove();
    }

    private static bool SameDefinition(S.Column left, S.Column right)
    {
        var a = (S.Column)left.CloneNode(true);
        var b = (S.Column)right.CloneNode(true);
        a.Min = a.Max = b.Min = b.Max = 1;
        return a.OuterXml == b.OuterXml;
    }
}
