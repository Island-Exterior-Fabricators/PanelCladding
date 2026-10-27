using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;

namespace PanelCladdingNamingConventionSmoke;

// PCtemplatePNL on generated sheets laid out like XYZ (one-letter frame configs, three-digit
// cladding) and ABC (two-letter frame configs, two-digit cladding).
internal static partial class Program
{
    private static readonly PanelNamingFormat XyzFormat = new("A", "A", "01", "001", "01");
    private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";

    private static void VerifyTemplate()
    {
        string directory = Path.Combine(Path.GetTempPath(), "PanelCladdingNamingTemplate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            VerifyXyzTemplate(directory);
            VerifyAbcTemplate(directory);
            VerifyTemplateRefusals(directory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void VerifyXyzTemplate(string directory)
    {
        string path = Path.Combine(directory, "XYZ - Naming Convention.xlsx");
        CreateXyzWorkbook(path);
        Dictionary<string, string> before = Snapshot(path);
        var template = new OpenXmlPanelNamingTemplate();
        PanelNamingTemplateState state = Required(template.Inspect(path));
        Require(state.RecordedFormat.Length == 0 && state.WrittenCodes["Frame-Config"].SequenceEqual(new[] { "A" }) &&
                state.WrittenCodes["Wall-Type"].Count == 13 && state.WrittenCodes["Cladding"].Count == 28 &&
                state.WrittenCodes["Frame-Type"].Count == 5 && state.WrittenCodes["Definition"].Count == 28,
            "Inspect finds the codes that have values under each title.");
        Require(OpenXmlPanelNamingTemplate.Misfits(state, "Frame-Config", "AA").SequenceEqual(new[] { "A" }) &&
                OpenXmlPanelNamingTemplate.Misfits(state, "Cladding", "01").Count == 28 &&
                OpenXmlPanelNamingTemplate.Misfits(state, "Frame-Type", "01").Count == 0,
            "Formats that written codes do not fit are reported.");

        PanelNamingTemplateResult result = Required(template.Apply(path, XyzFormat));
        Dictionary<string, string> after = Snapshot(path);
        string[] added = after.Keys.Except(before.Keys).Order(StringComparer.Ordinal).ToArray();
        string[] expectedAdded = Enumerable.Range(14, 23).Select(row => $"J{row}")
            .Concat(Enumerable.Range(26, 11).Select(row => $"H{row}")).Order(StringComparer.Ordinal).ToArray();
        Require(result.Changed && added.SequenceEqual(expectedAdded), "Only the placeholder codes are added: " + string.Join(" ", added));
        Require(before.All(item => after.TryGetValue(item.Key, out string? value) && value == item.Value),
            "Every existing cell keeps its value, type and style: " +
            string.Join(" ", before.Where(item => !after.TryGetValue(item.Key, out string? value) || value != item.Value).Select(item => item.Key)));
        using (SpreadsheetDocument document = SpreadsheetDocument.Open(path, false))
        {
            string Text(string reference) => CellText(document, "PNL", reference);
            Require(string.Concat(Enumerable.Range(13, 24).Select(row => Text($"J{row}"))) == Letters && Text("K13") == "0H 0V",
                "Frame-Config keeps A = 0H 0V and gets B..Z placeholders without I/O.");
            Require(string.Concat(Enumerable.Range(13, 24).Select(row => Text($"H{row}"))) == Letters && Text("I25") == "5" &&
                    FindCell(document, "PNL", "I25")?.DataType is null,
                "Wall-Type placeholders run to Z; the numeric value stays a number.");
            Require(StyleOf(document, "J14") == StyleOf(document, "J13") && !Struck(document, "H26"),
                "Placeholders take the plain style of the table's codes.");
            Require(Merges(document).Order(StringComparer.Ordinal).SequenceEqual(XyzMerges.Order(StringComparer.Ordinal)),
                "Merges are unchanged: " + string.Join(" ", Merges(document)));
            Require(FormatProperty(document) == XyzFormat.Serialize(), "The format is recorded in the workbook.");
            string[] errors = new OpenXmlValidator().Validate(document).Select(error => $"{error.Path?.XPath}: {error.Description}").ToArray();
            Require(errors.Length == 0, "The configured workbook must be schema-valid: " + string.Join("; ", errors));
        }
        byte[] configured = File.ReadAllBytes(path);
        PanelNamingTemplateResult again = Required(template.Apply(path, XyzFormat));
        Require(!again.Changed && File.ReadAllBytes(path).SequenceEqual(configured), "Running PCtemplatePNL again changes nothing.");

        // The save-time sync reads the format and leaves the configured sheet as it is.
        var workbook = new OpenXmlPanelNamingConventionWorkbook();
        PanelNamingConventionSheetContent sheet = Required(workbook.Read(path));
        PanelNamingFormat format = Required(PanelNamingFormat.Parse(sheet.RecordedFormat));
        Require(format == XyzFormat && sheet.FrameConfigs.Count == 1 && sheet.FrameConfigs["A"] == "0H 0V",
            "The sync reads the recorded format and the one-letter Frame-Config row.");
        var registry = new PanelFrameConfigCodeRegistry(format.FrameConfig);
        foreach ((string code, string config) in sheet.FrameConfigs) Require(registry.Register(code, config).Success, "Register " + code);
        Require(Resolve(registry, "0H 0V", expectAssigned: false) == "A", "0H 0V keeps code A.");
        PanelNamingConventionUsage usage = Usage(["F", "L"], ["A"], ["1A.1", "1A.2", "1C.1", "1C.2", "1C.4", "3A.2"]);
        PanelNamingConventionTables tables = PanelNamingConventionService.BuildTables(registry.Entries, sheet, usage, "XYZ", format);
        OperationResponse unchanged = workbook.Write(path, tables);
        Require(unchanged.Success && unchanged.Message == OpenXmlPanelNamingConventionWorkbook.UnchangedMessage &&
                File.ReadAllBytes(path).SequenceEqual(configured),
            "Saving the STN model leaves the configured sheet byte-for-byte unchanged: " + unchanged.Message);

        // A new configuration takes the first placeholder in place; nothing moves.
        Require(Resolve(registry, "1H 0V", expectAssigned: true) == "B", "The next configuration gets B.");
        tables = PanelNamingConventionService.BuildTables(registry.Entries, Required(workbook.Read(path)),
            Usage(["F", "L"], ["A", "B"], ["1A.1", "1A.2", "1C.1", "1C.2", "1C.4", "3A.2", "2C.1"]), "XYZ", format);
        Require(workbook.Write(path, tables).Success, "Write with a new frame config and wall type.");
        Dictionary<string, string> updated = Snapshot(path);
        string[] changedCells = updated.Where(item => !after.TryGetValue(item.Key, out string? value) || value != item.Value)
            .Select(item => item.Key).Order(StringComparer.Ordinal).ToArray();
        Require(changedCells.SequenceEqual(new[] { "I26", "K14" }) && updated.Count == after.Count + 2,
            "Only the filled placeholder rows change: " + string.Join(" ", changedCells));
        using (SpreadsheetDocument document = SpreadsheetDocument.Open(path, false))
        {
            Require(CellText(document, "PNL", "J14") == "B" && CellText(document, "PNL", "K14") == "1H 0V" &&
                    CellText(document, "PNL", "H26") == "P" && CellText(document, "PNL", "I26") == "2C.1" &&
                    CellText(document, "PNL", "J15") == "C" && CellText(document, "PNL", "H36") == "Z",
                "B and P are filled where their placeholders were; later placeholders stay.");
        }
        Console.WriteLine("[OK] PCtemplatePNL on a XYZ-like sheet adds only placeholders and the format; the sync then leaves it unchanged");
    }

    private static void VerifyAbcTemplate(string directory)
    {
        string path = Path.Combine(directory, "ABC - Naming Convention.xlsx");
        CreateAbcWorkbook(path);
        var template = new OpenXmlPanelNamingTemplate();
        var abc001 = new PanelNamingFormat("A", "AA", "01", "001", "01");
        PanelNamingTemplateResult result = Required(template.Apply(path, abc001));
        using (SpreadsheetDocument document = SpreadsheetDocument.Open(path, false))
        {
            string Text(string reference) => CellText(document, "PNL", reference);
            string[] merges = Merges(document);
            Require(result.Changed && new[] { "B2:W3", "D7:F7", "H5:P5", "H6:R6", "J7:K7", "L7:M7", "N7:P7", "Q7:R7", "T7:W7", "R11:S11", "T11:W11" }
                    .All(merges.Contains) && merges.Length == 11,
                "Cladding grows to N:P; merges reaching it grow, merges to its right move: " + string.Join(" ", merges));
            Require(Text("Q7") == "Definition" && Text("S7") == "-" && Text("T7") == "Seq" && Text("R11") == "*",
                "Cells to the right of Cladding move one column.");
            Require(string.Join("|", "JKLMNOPQRSTUVW".Select(column => Text($"{column}8"))) == "A|A|0|1|0|0|1|0|1|-|0|0|0|1" &&
                    FindCell(document, "PNL", "P8")?.DataType is null,
                "The example row shows AA 01 001 01, digits as numbers.");
            Require(ColumnWidth(document, 16) == 30 && ColumnWidth(document, 15) == 30 && ColumnWidth(document, 18) == 40,
                "The new column takes the Cladding width; later widths move with their columns.");
            Require(Text("L12") == "Frame-Type" && Text("N12") == "Cladding" && Text("Q12") == "Definition" &&
                    StyleOf(document, "L12") == StyleOf(document, "J12"),
                "Missing table titles are added under their headers in the title style.");
            Require(Text("L13") == "01" && Text("L36") == "24" && FindCell(document, "PNL", "L22")?.DataType is null &&
                    Text("N13") == "001" && Text("N36") == "024" && Text("Q13") == "01" && Text("J14") == "BA" && Text("J37") == string.Empty,
                "Digit tables get 24 placeholders; grouped AA frame configs get none.");
            Require(string.Concat(Enumerable.Range(13, 24).Select(row => Text($"H{row}"))) == Letters,
                "Wall-Type placeholders follow the written codes to Z.");
            string[] errors = new OpenXmlValidator().Validate(document).Select(error => $"{error.Path?.XPath}: {error.Description}").ToArray();
            Require(errors.Length == 0, "The widened workbook must be schema-valid: " + string.Join("; ", errors));
        }
        Require(!Required(template.Apply(path, abc001)).Changed, "A widened sheet is stable.");

        // Back to two-digit cladding: P only held the example digit and placeholders, so it goes.
        PanelNamingTemplateResult narrowed = Required(template.Apply(path, PanelNamingFormat.Legacy));
        using (SpreadsheetDocument document = SpreadsheetDocument.Open(path, false))
        {
            string Text(string reference) => CellText(document, "PNL", reference);
            string[] merges = Merges(document);
            Require(narrowed.Changed && new[] { "B2:V3", "H5:O5", "H6:Q6", "N7:O7", "P7:Q7", "S7:V7", "Q11:R11", "S11:V11" }.All(merges.Contains),
                "Cladding shrinks back to N:O: " + string.Join(" ", merges));
            Require(Text("N8") == "0" && Text("O8") == "1" && Text("P7") == "Definition" && Text("P12") == "Definition" &&
                    Text("N13") == "01" && Text("N36") == "24" && Text("P13") == "01",
                "Placeholders renumber to the new format; Definition moves back.");
            Require(ColumnWidth(document, 15) == 30 && ColumnWidth(document, 17) == 40, "Widths move back.");
            string[] errors = new OpenXmlValidator().Validate(document).Select(error => $"{error.Path?.XPath}: {error.Description}").ToArray();
            Require(errors.Length == 0, "The narrowed workbook must be schema-valid: " + string.Join("; ", errors));
        }
        Console.WriteLine("[OK] PCtemplatePNL widens and narrows segments, moving cells, merges and widths; titles and placeholders added");
    }

    private static void VerifyTemplateRefusals(string directory)
    {
        var template = new OpenXmlPanelNamingTemplate();
        string path = Path.Combine(directory, "refuse.xlsx");
        CreateAbcWorkbook(path);
        byte[] original = File.ReadAllBytes(path);
        RequireFailure(template.Apply(path, new PanelNamingFormat("A", "A", "01", "01", "01")), "PANEL_NAMING_TEMPLATE_CODES_DO_NOT_FIT");
        Require(File.ReadAllBytes(path).SequenceEqual(original), "Codes that would not fit leave the workbook untouched.");

        CreateXyzWorkbook(path, definitionCodes: ["001", "002"]);
        PanelNamingTemplateResult skipped = Required(template.Apply(path, XyzFormat));
        using (SpreadsheetDocument document = SpreadsheetDocument.Open(path, false))
        {
            Require(skipped.Notes.Any(note => note.StartsWith("Definition left as it is", StringComparison.Ordinal)) &&
                    CellText(document, "PNL", "Q13") == "001" && CellText(document, "PNL", "Q15") == string.Empty,
                "Definition codes that do not fit 01 leave Definition alone.");
        }

        CreateAbcWorkbook(path, formula: true);
        original = File.ReadAllBytes(path);
        RequireFailure(template.Apply(path, new PanelNamingFormat("A", "AA", "01", "001", "01")), "PANEL_NAMING_TEMPLATE_SHIFT_UNSUPPORTED");
        Require(File.ReadAllBytes(path).SequenceEqual(original), "A sheet with formulas is not shifted.");
        Require(Required(template.Apply(path, PanelNamingFormat.Legacy)).Changed, "Without a shift the same sheet is configured.");

        CreateTitleWorkbook(path, ("E12", "Unit-Type"));
        RequireFailure(template.Inspect(path), "PANEL_NAMING_TEMPLATE_LAYOUT_INVALID");
        Console.WriteLine("[OK] PCtemplatePNL refuses formats written codes do not fit, sheets it cannot shift and missing headers");
    }

    private static readonly string[] XyzMerges =
    [
        "B2:W3", "D7:F7", "H5:P5", "H6:R6", "J7:K7", "J8:K8", "L7:M7", "N7:P7", "Q7:R7", "R11:S11", "T11:W11", "T7:W7", "O12:P12",
        .. Enumerable.Range(13, 28).Select(row => $"O{row}:P{row}")
    ];

    // XYZ as on 2026-10-26 (after the user's fix): one-letter wall and frame codes, Frame-Type
    // 01.., Cladding 001.. with merged descriptions, Definition 01.. (10 and up as numbers).
    private static void CreateXyzWorkbook(string path, string[]? definitionCodes = null)
    {
        var cells = new List<Cell>
        {
            Inline("B2", "Panel", 1), Inline("H5", "REVIT FAMILY", 1), Inline("H6", "CORE PNL", 1),
            Inline("B7", "Group", 1), Inline("D7", "Job Code", 1), Inline("H7", "Unit-Type", 1), Inline("I7", "Wall-Type", 1),
            Inline("J7", "Frame-Config", 1), Inline("L7", "Frame-Type", 1), Inline("N7", "Cladding", 1), Inline("Q7", "Definition", 1),
            Inline("S7", "-", 1), Inline("T7", "Seq", 1),
            Inline("B8", "PNL", 2), Inline("D8", "X", 2), Inline("E8", "Y", 2), Inline("F8", "Z", 2), Inline("H8", "F", 2), Inline("I8", "A", 2),
            Inline("J8", "A", 2), Number("L8", 0, 2), Number("M8", 1, 2), Number("N8", 0, 2), Number("O8", 0, 2), Number("P8", 1, 2),
            Number("Q8", 0, 2), Number("R8", 1, 2), Inline("S8", "-", 2), Number("T8", 0, 2), Inline("U8", "0", 2), Number("V8", 0, 2), Number("W8", 1, 2),
            Inline("I11", "*letter I,O not used", 0), Inline("R11", "*", 0), Inline("T11", "%", 0),
            Inline("E12", "Unit-Type", 1), Inline("F12", "Description", 1), Inline("H12", "Wall-Type", 1), Inline("I12", "Value", 1),
            Inline("J12", "Frame-Config", 1), Inline("K12", "Configuration", 1), Inline("L12", "Frame-Type", 1), Inline("N12", "Cladding", 1),
            Inline("Q12", "Definition", 1),
            Inline("E13", "C", 3), Inline("F13", "Curved", 3), Inline("E14", "F", 2), Inline("F14", "Flat", 0),
            Inline("E15", "L", 2), Inline("F15", "Two Face", 0), Inline("E16", "Z", 3), Inline("F16", "Three Face", 3),
            Inline("J13", "A", 2), Inline("K13", "0H 0V", 0)
        };
        string[] walls = ["1A.1", "1A.2", "1A.4", "1A.5", "1C.1", "1C.2", "1C.4", "2B.2", "2B.1", "3A.1", "3A.2", "3A.4", "5"];
        bool[] struckWalls = [false, false, true, true, false, false, false, true, true, true, false, true, true];
        for (int index = 0; index < walls.Length; index++)
        {
            uint row = 13 + (uint)index;
            cells.Add(Inline($"H{row}", Letters[index].ToString(), struckWalls[index] ? 3u : 2u));
            cells.Add(walls[index] == "5" ? Number($"I{row}", 5, 3) : Inline($"I{row}", walls[index], struckWalls[index] ? 3u : 0u));
        }
        string[] frameTypes = ["NA", "oCornerL", "oCornerR", "iCornerL_oCornerR", "iCornerR"];
        definitionCodes ??= Enumerable.Range(1, 28).Select(value => value.ToString("00", CultureInfo.InvariantCulture)).ToArray();
        for (int index = 0; index < 28; index++)
        {
            uint row = 13 + (uint)index;
            int value = index + 1;
            cells.Add(value < 10 ? Inline($"L{row}", value.ToString("00", CultureInfo.InvariantCulture), 2) : Number($"L{row}", value, 2));
            if (index < frameTypes.Length) cells.Add(Inline($"M{row}", frameTypes[index], 0));
            cells.Add(Inline($"N{row}", value.ToString("000", CultureInfo.InvariantCulture), 2));
            cells.Add(Inline($"O{row}", $"GA-{value:000};GA-2010S-020", 0));
            if (index < definitionCodes.Length)
            {
                string code = definitionCodes[index];
                cells.Add(int.TryParse(code, out int number) && number >= 10 && code.Length == 2 ? Number($"Q{row}", number, 2) : Inline($"Q{row}", code, 2));
                cells.Add(Inline($"R{row}", $"parent;{value}", 0));
            }
        }
        WritePnl(path, cells, XyzMerges, [(6, 6, 32), (8, 8, 12), (11, 11, 16), (13, 13, 35), (16, 16, 50), (18, 18, 78)]);
    }

    // ABC as written by 1.1.4: Wall-Type I, Frame-Config J:K (AA codes), Frame-Type L:M,
    // Cladding N:O, Definition P:Q, Seq S:V; only the three sync tables have titles.
    private static void CreateAbcWorkbook(string path, bool formula = false)
    {
        var cells = new List<Cell>
        {
            Inline("B2", "Panel", 1), Inline("H5", "REVIT FAMILY", 1), Inline("H6", "CORE PNL", 1),
            Inline("B7", "Group", 1), Inline("D7", "Job Code", 1), Inline("H7", "Unit-Type", 1), Inline("I7", "Wall-Type", 1),
            Inline("J7", "Frame-Config", 1), Inline("L7", "Frame-Type", 1), Inline("N7", "Cladding", 1), Inline("P7", "Definition", 1),
            Inline("R7", "-", 1), Inline("S7", "Seq", 1),
            Inline("B8", "PNL", 2), Inline("D8", "A", 2), Inline("E8", "B", 2), Inline("F8", "C", 2), Inline("H8", "F", 2), Inline("I8", "A", 2),
            Inline("J8", "A", 2), Inline("K8", "A", 2), Number("L8", 0, 2), Number("M8", 1, 2), Number("N8", 0, 2), Number("O8", 1, 2),
            Number("P8", 0, 2), Number("Q8", 1, 2), Inline("R8", "-", 2), Number("S8", 0, 2), Inline("T8", "0", 2), Number("U8", 0, 2), Number("V8", 1, 2),
            Inline("I11", "*letter I,O not used", 0), Inline("Q11", "*", 0), Inline("S11", "%", 0),
            Inline("E12", "Unit-Type", 1), Inline("F12", "Description", 1), Inline("H12", "Wall-Type", 1), Inline("I12", "Value", 1),
            Inline("J12", "Frame-Config", 1), Inline("K12", "Configuration", 1),
            Inline("J13", "AA", 2), Inline("K13", "0H 0V", 0), Inline("J14", "BA", 2), Inline("K14", "0H 1V", 0)
        };
        for (int index = 0; index < 8; index++)
        {
            cells.Add(Inline($"H{13 + index}", Letters[index].ToString(), 2));
            cells.Add(Inline($"I{13 + index}", $"WT-{index + 1:00}", 0));
        }
        if (formula) cells.Add(new Cell { CellReference = "Y13", CellFormula = new CellFormula("1+1"), CellValue = new CellValue("2") });
        WritePnl(path, cells, ["B2:V3", "D7:F7", "H5:O5", "H6:Q6", "J7:K7", "L7:M7", "N7:O7", "P7:Q7", "S7:V7", "Q11:R11", "S11:V11"],
            [(15, 15, 30), (17, 17, 40)]);
    }

    private static void WritePnl(string path, IEnumerable<Cell> cells, IEnumerable<string> merges, (uint Min, uint Max, double Width)[] widths)
    {
        if (File.Exists(path)) File.Delete(path);
        using SpreadsheetDocument document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        WorkbookPart workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = new Stylesheet(
            new Fonts(new Font(new FontSize { Val = 11 }), new Font(new Bold(), new FontSize { Val = 11 }),
                new Font(new Strike(), new FontSize { Val = 11 }, new Color { Rgb = Grey })) { Count = 3 },
            new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 })) { Count = 2 },
            new Borders(new Border()) { Count = 1 },
            new CellFormats(
                new CellFormat(),
                new CellFormat { FontId = 1, ApplyFont = true },
                new CellFormat(new Alignment { Horizontal = HorizontalAlignmentValues.Center }) { ApplyAlignment = true },
                new CellFormat(new Alignment { Horizontal = HorizontalAlignmentValues.Center }) { FontId = 2, ApplyFont = true, ApplyAlignment = true })
            { Count = 4 });
        WorksheetPart part = workbookPart.AddNewPart<WorksheetPart>();
        var data = new SheetData();
        foreach (IGrouping<uint, Cell> row in cells.GroupBy(cell => uint.Parse(new string(cell.CellReference!.Value!.SkipWhile(char.IsLetter).ToArray()),
                     CultureInfo.InvariantCulture)).OrderBy(group => group.Key))
        {
            data.Append(new Row(row.OrderBy(cell => ColumnNumber(cell.CellReference!.Value!))) { RowIndex = row.Key });
        }
        var columns = new Columns(widths.Select(item => new Column { Min = item.Min, Max = item.Max, Width = item.Width, CustomWidth = true }));
        string[] mergeList = merges.ToArray();
        part.Worksheet = new Worksheet(columns, data, new MergeCells(mergeList.Select(merge => new MergeCell { Reference = merge })) { Count = (uint)mergeList.Length });
        workbookPart.Workbook.AppendChild(new Sheets(new Sheet { Name = "PNL", SheetId = 1, Id = workbookPart.GetIdOfPart(part) }));
    }

    private static int ColumnNumber(string reference) =>
        reference.TakeWhile(char.IsLetter).Aggregate(0, (sum, letter) => sum * 26 + letter - 'A' + 1);

    // Every cell as reference -> value, type and style.
    private static Dictionary<string, string> Snapshot(string path)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(path, false);
        Sheet sheet = document.WorkbookPart!.Workbook!.Sheets!.Elements<Sheet>().Single(item => item.Name == "PNL");
        var part = (WorksheetPart)document.WorkbookPart.GetPartById(sheet.Id!.Value!);
        return part.Worksheet!.Descendants<Cell>().ToDictionary(cell => cell.CellReference!.Value!,
            cell => $"{CellText(document, "PNL", cell.CellReference!.Value!)}|{cell.DataType?.Value}|{cell.StyleIndex?.Value ?? 0}");
    }

    private static string FormatProperty(SpreadsheetDocument document) =>
        document.CustomFilePropertiesPart?.Properties?.Elements<DocumentFormat.OpenXml.CustomProperties.CustomDocumentProperty>()
            .FirstOrDefault(item => item.Name?.Value == PanelNamingFormat.PropertyName)?.InnerText ?? string.Empty;

    private static double ColumnWidth(SpreadsheetDocument document, uint column)
    {
        Sheet sheet = document.WorkbookPart!.Workbook!.Sheets!.Elements<Sheet>().Single(item => item.Name == "PNL");
        var part = (WorksheetPart)document.WorkbookPart.GetPartById(sheet.Id!.Value!);
        return part.Worksheet!.Descendants<Column>().FirstOrDefault(item => item.Min!.Value <= column && item.Max!.Value >= column)?.Width?.Value ?? 0;
    }
}
