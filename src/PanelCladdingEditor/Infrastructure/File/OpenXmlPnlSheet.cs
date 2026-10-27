using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.VariantTypes;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace PanelCladdingEditor.Infrastructure.PanelCladding;

// Shared access to the PNL sheet of "<PROJECT> - Naming Convention.xlsx": rows 7/8 hold the
// segment headers and the example code, row 12 the table titles, codes start in row 13.
internal static class OpenXmlPnlSheet
{
    public const string SheetName = "PNL";
    public const uint HeaderRow = 7;
    public const uint ExampleRow = 8;
    public const uint TitleRow = 12;
    public const uint FirstDataRow = 13;
    private const string CustomPropertyFormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}";

    public static OperationResponse<WorksheetPart> FindSheet(SpreadsheetDocument document)
    {
        WorkbookPart? workbookPart = document.WorkbookPart;
        S.Sheet? sheet = workbookPart?.Workbook?.Sheets?.Elements<S.Sheet>()
            .FirstOrDefault(item => string.Equals(item.Name?.Value, SheetName, StringComparison.OrdinalIgnoreCase));
        return sheet?.Id?.Value is string id && workbookPart!.GetPartById(id) is WorksheetPart part
            ? OperationResponse<WorksheetPart>.Ok(part)
            : OperationResponse<WorksheetPart>.Fail($"PANEL_NAMING_CONVENTION_SHEET_MISSING: {SheetName}");
    }

    // The PNL format text recorded by PCtemplatePNL (custom property); empty when none.
    public static string ReadFormatProperty(SpreadsheetDocument document) =>
        document.CustomFilePropertiesPart?.Properties?.Elements<CustomDocumentProperty>()
            .FirstOrDefault(item => item.Name?.Value == PanelNamingFormat.PropertyName)?.InnerText.Trim() ?? string.Empty;

    public static void WriteFormatProperty(SpreadsheetDocument document, string text)
    {
        CustomFilePropertiesPart part = document.CustomFilePropertiesPart ?? document.AddCustomFilePropertiesPart();
        part.Properties ??= new Properties();
        CustomDocumentProperty? property = part.Properties.Elements<CustomDocumentProperty>()
            .FirstOrDefault(item => item.Name?.Value == PanelNamingFormat.PropertyName);
        if (property is null)
        {
            int id = part.Properties.Elements<CustomDocumentProperty>().Select(item => item.PropertyId?.Value ?? 1).DefaultIfEmpty(1).Max() + 1;
            property = new CustomDocumentProperty { FormatId = CustomPropertyFormatId, PropertyId = id, Name = PanelNamingFormat.PropertyName };
            part.Properties.AppendChild(property);
        }
        property.RemoveAllChildren();
        property.AppendChild(new VTLPWSTR(text));
        part.Properties.Save();
    }

    public static IEnumerable<S.Row> DataRows(S.SheetData sheetData) =>
        sheetData.Elements<S.Row>().Where(row => (row.RowIndex?.Value ?? 0) >= FirstDataRow);

    public static S.Row? FindRow(S.SheetData sheetData, uint rowIndex) =>
        sheetData.Elements<S.Row>().FirstOrDefault(row => row.RowIndex?.Value == rowIndex);

    public static S.Row InsertRow(S.SheetData sheetData, uint rowIndex)
    {
        var row = new S.Row { RowIndex = rowIndex };
        S.Row? next = sheetData.Elements<S.Row>().FirstOrDefault(item => (item.RowIndex?.Value ?? 0) > rowIndex);
        return next is null ? sheetData.AppendChild(row) : sheetData.InsertBefore(row, next);
    }

    public static S.Cell? FindCell(S.Row row, string column) =>
        row.Elements<S.Cell>().FirstOrDefault(cell => ColumnIndex(cell.CellReference?.Value) == ColumnIndex(column));

    public static S.Cell? FindCell(S.SheetData sheetData, string column, uint rowIndex) =>
        FindRow(sheetData, rowIndex) is S.Row row ? FindCell(row, column) : null;

    public static string CellText(WorkbookPart workbookPart, S.Row row, string column) =>
        FindCell(row, column) is S.Cell cell ? CellText(workbookPart, cell) : string.Empty;

    public static string CellText(WorkbookPart workbookPart, S.Cell? cell)
    {
        if (cell is null) return string.Empty;
        if (cell.DataType?.Value == S.CellValues.InlineString) return cell.InlineString?.InnerText ?? string.Empty;
        if (cell.DataType?.Value == S.CellValues.SharedString &&
            int.TryParse(cell.CellValue?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
        {
            return workbookPart.SharedStringTablePart?.SharedStringTable?.ElementAtOrDefault(index)?.InnerText ?? string.Empty;
        }
        return cell.CellValue?.Text ?? string.Empty;
    }

    public static void Upsert(S.Row row, S.Cell cell)
    {
        int column = ColumnIndex(cell.CellReference!.Value!);
        S.Cell? existing = row.Elements<S.Cell>().FirstOrDefault(item => ColumnIndex(item.CellReference?.Value) == column);
        if (existing is not null)
        {
            row.ReplaceChild(cell, existing);
            return;
        }
        S.Cell? next = row.Elements<S.Cell>().FirstOrDefault(item => ColumnIndex(item.CellReference?.Value) > column);
        if (next is null) row.Append(cell);
        else row.InsertBefore(cell, next);
    }

    public static S.Cell InlineCell(string column, uint rowIndex, string value, uint styleIndex) => new()
    {
        CellReference = column + rowIndex.ToString(CultureInfo.InvariantCulture),
        DataType = S.CellValues.InlineString,
        StyleIndex = styleIndex,
        InlineString = new S.InlineString(new S.Text(value) { Space = SpaceProcessingModeValues.Preserve })
    };

    // A number when the text is one exactly as Excel would show it ("5", "12", "1.5"); otherwise
    // text, so codes such as "01" keep their leading zero.
    public static S.Cell ValueCell(string column, uint rowIndex, string value, uint styleIndex) =>
        IsPlainNumber(value)
            ? new S.Cell
            {
                CellReference = column + rowIndex.ToString(CultureInfo.InvariantCulture),
                StyleIndex = styleIndex,
                CellValue = new S.CellValue(value)
            }
            : InlineCell(column, rowIndex, value, styleIndex);

    public static bool IsPlainNumber(string value) =>
        value.Length is > 0 and <= 15 &&
        decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal number) &&
        number.ToString(CultureInfo.InvariantCulture) == value;

    public static int ColumnIndex(string? reference)
    {
        int result = 0;
        foreach (char letter in (reference ?? string.Empty).TakeWhile(char.IsLetter))
        {
            result = result * 26 + (char.ToUpperInvariant(letter) - 'A' + 1);
        }
        return result;
    }

    public static string ColumnName(int index)
    {
        string name = string.Empty;
        for (; index > 0; index = (index - 1) / 26)
        {
            name = (char)('A' + (index - 1) % 26) + name;
        }
        return name;
    }

    public static uint RowOf(string reference) =>
        uint.TryParse(new string(reference.SkipWhile(char.IsLetter).ToArray()), NumberStyles.None,
            CultureInfo.InvariantCulture, out uint row) ? row : 0;

    public static string Normalize(string text) => new string(text.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    // Edits a copy of the workbook and moves it over the original in one step, as Excel saves.
    // File.Replace needs a backup file on the project share and a cloud drive (Egnyte) can hold
    // that backup so it cannot be removed. An open workbook refuses the move and is reported as
    // locked; a workbook changed meanwhile is left alone. When the edit reports no change the
    // original is kept and unchangedMessage returned.
    public static OperationResponse ReplaceWorkbook(string path, Func<SpreadsheetDocument, OperationResponse<bool>> edit,
        string lockedAdvice, string unchangedMessage)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        string tempPath = Path.Combine(directory, $".{Path.GetFileNameWithoutExtension(path)}.naming.{Guid.NewGuid():N}.tmp.xlsx");
        try
        {
            var original = new FileInfo(path);
            long originalLength = original.Length;
            DateTime originalWriteUtc = original.LastWriteTimeUtc;
            File.Copy(path, tempPath);
            using (SpreadsheetDocument document = SpreadsheetDocument.Open(tempPath, true))
            {
                OperationResponse<bool> edited = edit(document);
                if (!edited.Success)
                {
                    return edited;
                }
                if (!edited.Data)
                {
                    return OperationResponse.Ok(unchangedMessage);
                }
            }
            var latest = new FileInfo(path);
            if (latest.Length != originalLength || latest.LastWriteTimeUtc != originalWriteUtc)
            {
                return OperationResponse.Fail($"PANEL_NAMING_CONVENTION_CHANGED_DURING_SAVE: {Path.GetFileName(path)}");
            }
            File.Move(tempPath, path, overwrite: true);
            return OperationResponse.Ok();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return OperationResponse.Fail(
                $"PANEL_NAMING_CONVENTION_LOCKED: {Path.GetFileName(path)} is open or locked ({exception.Message}). {lockedAdvice}");
        }
        catch (Exception exception) when (exception is OpenXmlPackageException or InvalidDataException)
        {
            return OperationResponse.Fail($"PANEL_NAMING_CONVENTION_WRITE_FAILED: {Path.GetFileName(path)}: {exception.Message}");
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    // Temp or backup files of an earlier save (".<name>.naming.<guid>.*.tmp.xlsx") that a sync
    // client held at the time are removed once they are over a minute old.
    public static void RemoveStaleOwnFiles(string path)
    {
        try
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
            DateTime cutoff = DateTime.UtcNow.AddMinutes(-1);
            foreach (string file in Directory.EnumerateFiles(directory, $".{Path.GetFileNameWithoutExtension(path)}.naming.*.tmp.xlsx"))
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff && File.GetCreationTimeUtc(file) < cutoff) TryDelete(file);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort; the next save tries again.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Best effort for operation-owned temporary files only.
        }
    }
}

// "Unused" styles are the template style with a struck-through grey font. Equal fonts and
// formats are reused, so repeated saves do not grow the stylesheet.
internal sealed class PnlStyleCache(S.Stylesheet? stylesheet)
{
    private const string GreyRgb = "FF808080";
    private static readonly string[] FontChildOrder =
        ["b", "i", "strike", "condense", "extend", "outline", "shadow", "u", "vertAlign", "sz", "color", "name", "family", "charset", "scheme"];

    public bool IsStruck(uint style) => FontOf(style)?.GetFirstChild<S.Strike>() is not null;

    public uint Plain(uint style) => IsStruck(style) ? Derive(style, struck: false, unbold: false) : style;

    // A data style taken from a title cell: plain and not bold.
    public uint Body(uint style) =>
        IsStruck(style) || FontOf(style)?.GetFirstChild<S.Bold>() is not null ? Derive(style, struck: false, unbold: true) : style;

    public uint Struck(uint style) => IsStruck(style) ? style : Derive(style, struck: true, unbold: false);

    public uint WithState(uint style, bool struck) => struck ? Struck(style) : Plain(style);

    public void Save() => stylesheet?.Save();

    private S.CellFormat? FormatOf(uint style) =>
        stylesheet?.CellFormats?.Elements<S.CellFormat>().ElementAtOrDefault((int)style);

    private S.Font? FontOf(uint style) =>
        stylesheet?.Fonts?.Elements<S.Font>().ElementAtOrDefault((int)(FormatOf(style)?.FontId?.Value ?? 0));

    private uint Derive(uint style, bool struck, bool unbold)
    {
        if (stylesheet?.CellFormats is null || stylesheet.Fonts is null) return style;
        var font = (S.Font?)FontOf(style)?.CloneNode(true) ?? new S.Font();
        font.RemoveAllChildren<S.Strike>();
        if (unbold) font.RemoveAllChildren<S.Bold>();
        foreach (S.Color color in font.Elements<S.Color>().Where(color => struck || color.Rgb?.Value == GreyRgb).ToArray())
        {
            color.Remove();
        }
        if (struck)
        {
            font.Append(new S.Strike());
            font.Append(new S.Color { Rgb = GreyRgb });
        }
        OpenXmlElement[] children = font.ChildElements
            .OrderBy(child => Array.IndexOf(FontChildOrder, child.LocalName) is int order and >= 0 ? order : FontChildOrder.Length)
            .Select(child => child.CloneNode(true)).ToArray();
        font.RemoveAllChildren();
        font.Append(children);
        var format = (S.CellFormat?)FormatOf(style)?.CloneNode(true) ?? new S.CellFormat();
        format.FontId = FindOrAppend(stylesheet.Fonts, font);
        format.ApplyFont = true;
        stylesheet.Fonts.Count = (uint)stylesheet.Fonts.Elements<S.Font>().Count();
        uint index = FindOrAppend(stylesheet.CellFormats, format);
        stylesheet.CellFormats.Count = (uint)stylesheet.CellFormats.Elements<S.CellFormat>().Count();
        return index;
    }

    private static uint FindOrAppend<T>(OpenXmlCompositeElement list, T item) where T : OpenXmlElement
    {
        string xml = item.OuterXml;
        uint index = 0;
        foreach (T existing in list.Elements<T>())
        {
            if (existing.CloneNode(true).OuterXml == xml) return index;
            index++;
        }
        list.Append(item);
        return index;
    }
}
