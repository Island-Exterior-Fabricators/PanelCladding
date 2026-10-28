using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;

namespace PanelCladdingAttributeConventionsSmoke;

internal static class Program
{
    private const string FrameType = "CW_1.09_FRAME_TYPE";
    private const string Payload = "{\"v\":1,\"f\":{\"FRM-0\":[\"1D-LEGACY\"]},\"s\":[]}";
    private const string AbcPlane = "{595.875, 358.25, 376.1875};{1,0,0};{0,0,1}";
    private const string XyzFormat = "Wall-Type=A; Frame-Config=A; Frame-Type=01; Cladding=001; Definition=01";

    private static void Main()
    {
        VerifyKeyNames();
        VerifyFrameAssignmentsMoved();
        VerifyPlaneFormat();
        VerifyPlaneAutoValue();
        VerifyFrameTypeTable();
        VerifyFrameTypeWorkbook();
        VerifyPidPlaceholders();
        VerifyRoles();
        VerifyShortNames();
        Console.WriteLine("PASS: panel cladding attribute conventions regression");
    }

    private static void VerifyKeyNames()
    {
        Require(PanelCladdingKeyService.FrameTypeKey == FrameType &&
                PanelCladdingKeyService.FrameAssignmentsKey == "UD_FRAME_ASSIGNMENTS" &&
                PanelCladdingKeyService.PlaneKey == "UD_PLANE",
            "Key names: CW_1.09_FRAME_TYPE (hand-filled), UD_FRAME_ASSIGNMENTS, UD_PLANE.");
        var keys = new PanelCladdingKeyService();
        Require(!keys.IsClearableCladdingAssignmentKey(FrameType) && !keys.IsClearableCladdingAssignmentKey("cw_1.09_frame_type") &&
                keys.IsClearableCladdingAssignmentKey("UD_FRAME_ASSIGNMENTS"),
            "PCClear removes UD_FRAME_ASSIGNMENTS and leaves CW_1.09_FRAME_TYPE.");
        Console.WriteLine("[OK] key names and clear scope");
    }

    // XYZ corner panels hold text in CW_1.09; the payload lives in UD_FRAME_ASSIGNMENTS.
    private static void VerifyFrameAssignmentsMoved()
    {
        var service = new PanelFrameAssignmentService();
        var topology = new PanelCladdingTopologyState();
        PanelFrameAssignmentState moved = Required(service.Decode(
            new Dictionary<string, string> { ["UD_FRAME_ASSIGNMENTS"] = Payload, [FrameType] = "oCornerL" }, 0, 0, topology));
        Require(moved.FrameAssignments["FRM-0"].SequenceEqual(["1D-LEGACY"]) &&
                moved.Definitions["1D-LEGACY"].CalculationValue == 1d,
            "The assignment payload is decoded from UD_FRAME_ASSIGNMENTS.");
        Require(Required(service.Decode(new Dictionary<string, string> { [FrameType] = Payload }, 0, 0, topology)).IsEmpty &&
                Required(service.Decode(new Dictionary<string, string> { [FrameType] = "iCornerL_oCornerR" }, 0, 0, topology)).IsEmpty,
            "CW_1.09_FRAME_TYPE is never decoded as a payload.");
        Require(service.Decode(new Dictionary<string, string>
                {
                    ["CW_2.09_FRAME_ASSIGNMENTS"] = Payload
                }, 0, 0, topology) is { Success: true, Data.IsEmpty: false },
            "The legacy CW_2.09 payload still reads when UD_FRAME_ASSIGNMENTS is blank.");

        var text = new Dictionary<string, string>
        {
            [PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = "100",
            [FrameType] = "oCornerR",
            ["cw_1.09_frame_type"] = "different case, different value"
        };
        Require(new PanelCladdingKeyService().Parse(text, 80, 231.25623, 0.001).Success &&
                PanelCladdingKeyService.ValidateFrameAttributeNames(text).Success,
            "A XYZ panel with a CW_1.09 frame type loads (it used to fail as PAYLOAD_INVALID).");
        var conflict = new Dictionary<string, string> { ["UD_FRAME_ASSIGNMENTS"] = Payload, ["ud_frame_assignments"] = "{}" };
        RequireFailure(PanelCladdingKeyService.ValidateFrameAttributeNames(conflict), "PANEL_FRAME_ATTRIBUTE_CONFLICT");
        Console.WriteLine("[OK] frame assignments read from UD_FRAME_ASSIGNMENTS; CW_1.09 text loads");
    }

    private static void VerifyPlaneFormat()
    {
        var abc = new PanelPlaneFrame(new(595.875, 358.25, 376.1875), new(1, 0, 0), new(0, 0, 1));
        Require(PanelCladdingPlaneFormat.Format(abc) == AbcPlane, "UD_PLANE keeps the form already stored in ABC.");
        Require(PanelCladdingPlaneFormat.TryParse(AbcPlane, out PanelPlaneFrame parsed) && parsed == abc,
            "A ABC value parses back to its frame.");
        Require(PanelCladdingPlaneFormat.TryParse("{0,0,0};{2,0,0};{0,0,3}", out PanelPlaneFrame scaled) &&
                scaled.XAxis == new PanelPoint3(1, 0, 0) && scaled.YAxis == new PanelPoint3(0, 0, 1),
            "Axes are unitized when read.");
        double s = Math.Sqrt(0.5);
        string angled = PanelCladdingPlaneFormat.Format(new(new(-1e-9, 1234.5678901, 0.0000004), new(s, -s, 0), new(0, 0, 1)));
        Require(angled == "{0, 1234.56789, 0};{0.707107,-0.707107,0};{0,0,1}",
            "Numbers round to six decimals without trailing zeros or negative zero: " + angled);
        Require(PanelCladdingPlaneFormat.Matches("{595.8750000, 358.25,376.1875};{1.0,0,0};{0,0,1.0}", abc, 0.001) &&
                PanelCladdingPlaneFormat.Matches(angled, new(new(0, 1234.5678901, 0), new(s, -s, 0), new(0, 0, 1)), 0.001),
            "Formatting and rounding differences describe the same frame.");
        Require(!PanelCladdingPlaneFormat.Matches("{595.885, 358.25, 376.1875};{1,0,0};{0,0,1}", abc, 0.001) &&
                !PanelCladdingPlaneFormat.Matches("{595.875, 358.25, 376.1875};{-1,0,0};{0,0,1}", abc, 0.001) &&
                !PanelCladdingPlaneFormat.Matches(" ", abc, 0.001) &&
                !PanelCladdingPlaneFormat.Matches("{1,2,3};{0,0,0};{0,0,1}", abc, 0.001),
            "A moved origin, a flipped axis, a blank or a degenerate value is not current.");
        Console.WriteLine("[OK] UD_PLANE format, parse and tolerance match");
    }

    private static void VerifyPlaneAutoValue()
    {
        Require(PanelCladdingAutoAttributeService.Keys.Contains("UD_PLANE") &&
                PanelCladdingAutoAttributeService.IsInputKey("ud_plane"),
            "UD_PLANE is an auto value; editing it by hand queues a refresh.");
        var frame = new PanelPlaneFrame(new(595.875, 358.25, 376.1875), new(1, 0, 0), new(0, 0, 1));
        var text = new Dictionary<string, string>
        {
            ["CW_2.00_UNIT_DIMENSION"] = "90.00000x305.00000", ["CW_2.01_UNIT_WIDTH"] = "90.00000",
            ["CW_2.02_UNIT_HEIGHT"] = "305.00000", ["CW_2.07_UNIT_AREA"] = "190.625 sqf",
            ["CW_6.01_PSF"] = "85", ["CW_6.02_UNIT_WEIGHT"] = "16203.125",
            ["UD_PLANE"] = "{595.8750, 358.25, 376.1875};{1.0,0,0};{0,0,1}"
        };
        IReadOnlyDictionary<string, string> desired =
            PanelCladdingAutoAttributeService.CreateValues(90, 305, 190.625, text, frame);
        Require(desired["UD_PLANE"] == AbcPlane && desired.Count == 6, "The desired values include UD_PLANE.");
        Require(PanelCladdingAutoAttributeService.Changes(desired, text, 0.001).Count == 0,
            "An equivalent stored UD_PLANE is not rewritten.");
        var stale = new Dictionary<string, string>(text) { ["UD_PLANE"] = "{825.125, 1093.75, 376.1875};{1,0,0};{0,0,1}" };
        Require(PanelCladdingAutoAttributeService.Changes(desired, stale, 0.001).Keys.SequenceEqual(["UD_PLANE"]),
            "A stale origin is refreshed (ABC's stored origins do not match the geometry).");
        var legacy = new Dictionary<string, string>(text) { ["Plane"] = AbcPlane };
        legacy.Remove("UD_PLANE");
        IReadOnlyDictionary<string, string> added = PanelCladdingAutoAttributeService.Changes(desired, legacy, 0.001);
        Require(added.Count == 1 && added["UD_PLANE"] == AbcPlane, "Plane is not read; UD_PLANE is added and Plane left alone.");
        var variant = new Dictionary<string, string>(text) { ["ud_plane"] = AbcPlane };
        variant.Remove("UD_PLANE");
        Require(PanelCladdingAutoAttributeService.Changes(desired, variant, 0.001).ContainsKey("UD_PLANE"),
            "A case variant is rewritten under UD_PLANE.");
        Require(PanelCladdingAutoAttributeService.CreateValues(90, 305, 190.625, text).Count == 5,
            "The values without a frame are unchanged.");
        Console.WriteLine("[OK] UD_PLANE auto value and minimal change set");
    }

    private static void VerifyFrameTypeTable()
    {
        PanelNamingFormat met = Required(PanelNamingFormat.Parse(XyzFormat));
        var sheet = new PanelNamingConventionSheetContent
        {
            FrameTypes = new Dictionary<string, string> { ["01"] = "NA", ["02"] = "oCornerL", ["03"] = "oCornerR" }
        };
        PanelNamingConventionTables tables = PanelNamingConventionService.BuildTables(
            new Dictionary<string, string>(), sheet, Usage(["oCornerL", "iCornerR"], blank: true), "XYZ", met);
        Require(Rows(tables.FrameTypes) == "01=NA+ 02=oCornerL+ 03=oCornerR- 04=iCornerR+" && tables.UncodedFrameTypes.Count == 0,
            "Sheet rows keep their codes, a new value takes the next code, NA stands for blank: " + Rows(tables.FrameTypes));
        PanelNamingConventionTables noBlank = PanelNamingConventionService.BuildTables(
            new Dictionary<string, string>(), sheet, Usage(["OCORNERL"], blank: false), "XYZ", met);
        Require(Rows(noBlank.FrameTypes) == "01=NA- 02=oCornerL+ 03=oCornerR-",
            "NA is unused without blank frame types; values match ignoring case: " + Rows(noBlank.FrameTypes));
        PanelNamingFormat letters = Required(PanelNamingFormat.Parse("Wall-Type=A; Frame-Config=AA; Frame-Type=A; Cladding=01"));
        string[] many = Enumerable.Range(1, PanelNamingFormat.Letters.Length + 1).Select(index => $"FT-{index}").ToArray();
        PanelNamingConventionTables exhausted = PanelNamingConventionService.BuildTables(
            new Dictionary<string, string>(), new PanelNamingConventionSheetContent(), Usage(many, blank: false), "ABC", letters);
        Require(exhausted.FrameTypes.Count == PanelNamingFormat.Letters.Length &&
                exhausted.UncodedFrameTypes.SequenceEqual([$"FT-{many.Length}"]) &&
                exhausted.FrameTypes[0].Code == "A" && exhausted.FrameTypes[1].Description == "FT-2",
            "Values are coded in natural order; one without a free code is reported.");
        Console.WriteLine("[OK] Frame-Type table codes, NA rule and exhausted format");
    }

    private static void VerifyFrameTypeWorkbook()
    {
        string directory = Path.Combine(Path.GetTempPath(), "PanelCladdingAttributeConventions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var workbook = new OpenXmlPanelNamingConventionWorkbook();
            PanelNamingFormat met = Required(PanelNamingFormat.Parse(XyzFormat));

            // ABC: Frame-Type header in row 7, no table yet. Nothing to record leaves the sheet alone.
            string abc = Path.Combine(directory, "ABC - Naming Convention.xlsx");
            CreateWorkbook(abc, xyzTable: false);
            PanelNamingConventionSheetContent abcSheet = Required(workbook.Read(abc));
            Require(abcSheet.FrameTypes.Count == 0, "An untitled Frame-Type table has no rows.");
            PanelNamingConventionTables none = PanelNamingConventionService.BuildTables(
                abcSheet.FrameConfigs, abcSheet, Usage([], blank: true), "ABC", PanelNamingFormat.Legacy);
            Require(workbook.Write(abc, none).Success, "ABC write without frame types.");
            using (SpreadsheetDocument document = SpreadsheetDocument.Open(abc, false))
            {
                Require(CellText(document, "L12").Length == 0, "No Frame-Type table is added while nothing is recorded.");
            }
            PanelNamingConventionTables one = PanelNamingConventionService.BuildTables(
                abcSheet.FrameConfigs, abcSheet, Usage(["oCornerL"], blank: true), "ABC", PanelNamingFormat.Legacy);
            Require(workbook.Write(abc, one).Success, "ABC write with a frame type.");
            using (SpreadsheetDocument document = SpreadsheetDocument.Open(abc, false))
            {
                Require(CellText(document, "L12") == "Frame-Type" && CellText(document, "M12") == "Value" &&
                        CellText(document, "L13") == "01" && CellText(document, "M13") == "oCornerL",
                    "The Frame-Type table is created under its row-7 header with the first code.");
                Require(CellText(document, "H13") == "A" && CellText(document, "I13") == "WT-01",
                    "Other tables are unchanged.");
            }
            Require(Required(workbook.Read(abc)).FrameTypes["01"] == "oCornerL", "The new table reads back.");

            // XYZ: titled table with NA, two values and placeholder rows.
            string xyzPath = Path.Combine(directory, "XYZ - Naming Convention.xlsx");
            CreateWorkbook(xyzPath, xyzTable: true);
            PanelNamingConventionSheetContent xyzSheet = Required(workbook.Read(xyzPath));
            Require(xyzSheet.FrameTypes.Count == 2 && xyzSheet.FrameTypes["01"] == "NA", "Placeholders are not values.");
            PanelNamingConventionTables xyzTables = PanelNamingConventionService.BuildTables(
                xyzSheet.FrameConfigs, xyzSheet, Usage(["oCornerL", "iCornerR"], blank: true), "XYZ", met);
            Require(workbook.Write(xyzPath, xyzTables).Success, "XYZ write.");
            using (SpreadsheetDocument document = SpreadsheetDocument.Open(xyzPath, false))
            {
                Require(CellText(document, "L15") == "03" && CellText(document, "M15") == "iCornerR" &&
                        CellText(document, "M16").Length == 0,
                    "A new value fills the first placeholder row.");
                Require(!Struck(document, "M13") && !Struck(document, "M14") && !Struck(document, "M15"),
                    "Used rows (NA for blank types) are not struck.");
            }
            Require(workbook.Write(xyzPath, xyzTables) is { Success: true, Message: OpenXmlPanelNamingConventionWorkbook.UnchangedMessage },
                "A current sheet is not rewritten.");
            PanelNamingConventionTables unused = PanelNamingConventionService.BuildTables(
                xyzSheet.FrameConfigs, Required(workbook.Read(xyzPath)), Usage(["iCornerR"], blank: false), "XYZ", met);
            Require(workbook.Write(xyzPath, unused).Success, "XYZ write with fewer types.");
            using (SpreadsheetDocument document = SpreadsheetDocument.Open(xyzPath, false))
            {
                Require(Struck(document, "M13") && Struck(document, "M14") && !Struck(document, "M15") &&
                        CellText(document, "M14") == "oCornerL",
                    "Unused rows are struck, never removed or rewritten.");
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
        Console.WriteLine("[OK] Frame-Type table in the PNL workbook (ABC without table, XYZ with table)");
    }

    private static void VerifyPidPlaceholders()
    {
        var setup = new PanelCladdingPidSetupService();
        var planner = new PanelCladdingPidPlanningService();
        var text = new Dictionary<string, string>
        {
            ["CW_2.06_PENETRATION_DIMENSION"] = "12x12",
            ["cw_7.00_blind_single"] = "",
            [FrameType] = "oCornerL",
            ["CW_1.10_REVIT_TYPE"] = " ",
            ["CW_1.12_INSTALL_SEQUENCE"] = "3",
            ["CW_1.11_PHASE"] = " ",
            ["CW_1.05_LOT"] = "MET-04.05",
            ["cw_1.05_lot"] = "other case"
        };
        PanelCladdingPidPanel panel = Rect(text);
        PanelCladdingPidAssignment assignment = Required(planner.CreatePlan(
            new("ABC", [panel.ObjectId], panel.ObjectId, panel.ObjectId), [panel], .00001, Math.PI / 180)).Panels.Single();
        IReadOnlyDictionary<string, string> writes = Required(setup.CreateWrites(assignment, panel));
        string[] untouched = ["CW_2.06_PENETRATION_DIMENSION", "CW_7.00_BLIND_SINGLE", FrameType, "CW_1.11_PHASE", "CW_1.05_LOT"];
        Require(untouched.All(key => !writes.Keys.Contains(key, StringComparer.OrdinalIgnoreCase)),
            "Existing keys (any case, blank or not) are not written: " + string.Join(", ", writes.Keys));
        Require(writes["CW_7.00_BLIND_DOUBLE"] == " " && writes["CW_1.10_CAD_TYPE"] == " " && writes["CW_1.00_PNL"] == " " &&
                !writes.ContainsKey("CW_1.12_INSTALL_SEQUENCE") && !writes.ContainsKey("CW_1.10_REVIT_TYPE"),
            "Missing setup keys get a blank placeholder; other keys are not added.");
        Require(writes["CW_1.01_PID"] == assignment.Pid && writes["CW_1.02_CID"] == assignment.Cid &&
                writes["CW_2.00_UNIT_DIMENSION"] == "80.00000x231.25623" && writes["CW_5.00_DEFINITION_A"] == "parent",
            "Identity, dimensions and the flat-panel role are still written.");
        Console.WriteLine("[OK] PCpid placeholders never overwrite existing keys");
    }

    private static void VerifyRoles()
    {
        Require(PanelCladdingCidService.ParentRole == "parent" && PanelCladdingCidService.ChildRole == "child", "Role constants are lowercase.");
        Require(PanelCladdingCidService.NormalizeRole(" Parent ") == "parent" && PanelCladdingCidService.NormalizeRole("Child 2") == "child 2" &&
                PanelCladdingCidService.NormalizeRole("p-shaped") == "p-shaped" && PanelCladdingCidService.NormalizeRole("") == "",
            "Roles are lowercased; other values are untouched.");
        var flat = new Dictionary<string, string>();
        PanelCladdingPidSetupService.ReserveRoleDefinition(flat,
            new Dictionary<string, string> { ["CW_1.06_UNIT_TYPE"] = "F", ["CW_5.00_DEFINITION_A"] = "Parent" });
        var corner = new Dictionary<string, string>();
        PanelCladdingPidSetupService.ReserveRoleDefinition(corner,
            new Dictionary<string, string> { ["CW_1.06_UNIT_TYPE"] = "L", ["CW_5.00_DEFINITION_A"] = "Child 1" });
        var note = new Dictionary<string, string>();
        PanelCladdingPidSetupService.ReserveRoleDefinition(note,
            new Dictionary<string, string> { ["CW_1.06_UNIT_TYPE"] = "L", ["CW_5.00_DEFINITION_A"] = "sill" });
        Require(flat["CW_5.00_DEFINITION_A"] == "parent" && corner["CW_5.00_DEFINITION_A"] == "child 1" &&
                note["CW_5.00_DEFINITION_A"] == " " && note["CW_5.00_DEFINITION_B"] == "sill",
            "PCpid stores lowercase roles and still reserves Definition A.");
        Require(PanelCladdingCidService.RoleSuffix(new Dictionary<string, string>
                    { ["CW_1.06_UNIT_TYPE"] = "L", ["CW_5.00_DEFINITION_A"] = "Child" }) == "_C" &&
                PanelCladdingCidService.RoleSuffix(new Dictionary<string, string>
                    { ["CW_1.06_UNIT_TYPE"] = "L", ["CW_5.00_DEFINITION_A"] = "parent" }) == "_P",
            "Role suffixes read any case.");
        Console.WriteLine("[OK] lowercase roles");
    }

    private static void VerifyShortNames()
    {
        (string Cid, string Name)[] cases =
        [
            ("CID-XYZ-S-04-36", "S-04-36"),
            ("CID-XYZ-S-02B-01_P", "S-02B-01_P"),
            ("CID-ABC-N2-05-05_C_0A", "N2-05-05_C_0A"),
            ("cid-abc-E1-01-02", "E1-01-02"),
            ("CID-ABC-W4-PF-05_INT-A0", "W4-PF-05_INT-A0"),
            ("CID-SOLO", "SOLO"),
            ("CUSTOM-NAME", "CUSTOM-NAME"),
            ("  CID-QRS-A1-01-01  ", "A1-01-01")
        ];
        foreach ((string cid, string name) in cases)
        {
            Require(PanelCladdingCidService.ShortName(cid) == name, $"ShortName('{cid}') = '{PanelCladdingCidService.ShortName(cid)}', expected '{name}'.");
        }
        Console.WriteLine("[OK] object names drop CID-<project>-");
    }

    private static PanelNamingConventionUsage Usage(IEnumerable<string> frameTypes, bool blank) => new()
    {
        UnitTypes = new HashSet<string> { "F" },
        FrameConfigCodes = new HashSet<string>(),
        WallTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "WT-01" },
        FrameTypes = new HashSet<string>(frameTypes, StringComparer.OrdinalIgnoreCase),
        HasBlankFrameType = blank
    };

    private static string Rows(IEnumerable<PanelNamingCodeRow> rows) =>
        string.Join(" ", rows.Select(row => $"{row.Code}={row.Description}{(row.Used ? "+" : "-")}"));

    // An 80 x 231.25623 flat panel facing +Y.
    private static PanelCladdingPidPanel Rect(IReadOnlyDictionary<string, string> userText)
    {
        PanelPoint3 Point(double x, double z) => new(-x, 0, z);
        return new(Guid.NewGuid(), new PanelPoint3(0, 1, 0),
            [Point(0, 0), Point(80, 0), Point(80, 231.25623), Point(0, 231.25623)],
            new Dictionary<string, string>(userText) { ["CW_1.06_UNIT_TYPE"] = "F" });
    }

    // A PNL sheet with the ABC code tables; xyzTable adds XYZ's titled Frame-Type table.
    private static void CreateWorkbook(string path, bool xyzTable)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        WorkbookPart workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = new Stylesheet(
            new Fonts(new Font(new FontSize { Val = 11 }), new Font(new Bold(), new FontSize { Val = 11 })) { Count = 2 },
            new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 })) { Count = 2 },
            new Borders(new Border()) { Count = 1 },
            new CellFormats(new CellFormat(), new CellFormat { FontId = 1, ApplyFont = true }) { Count = 2 });
        var rows = new List<Row>
        {
            NewRow(7, Inline("D7", "Job Code", 1), Inline("H7", "Unit-Type", 1), Inline("I7", "Wall-Type", 1),
                Inline("J7", "Frame-Config", 1), Inline("L7", "Frame-Type", 1), Inline("N7", "Cladding", 1)),
            NewRow(12, new[]
                {
                    Inline("E12", "Unit-Type", 1), Inline("F12", "Description", 1), Inline("H12", "Wall-Type", 1),
                    Inline("I12", "Value", 1), Inline("J12", "Frame-Config", 1), Inline("K12", "Configuration", 1)
                }.Concat(xyzTable ? [Inline("L12", "Frame-Type", 1)] : []).ToArray()),
            NewRow(13, new[] { Inline("H13", "A", 0), Inline("I13", "WT-01", 0) }
                .Concat(xyzTable ? [Inline("L13", "01", 0), Inline("M13", "NA", 0)] : []).ToArray())
        };
        if (xyzTable)
        {
            rows.Add(NewRow(14, Inline("L14", "02", 0), Inline("M14", "oCornerL", 0)));
            rows.Add(NewRow(15, Inline("L15", "03", 0)));
            rows.Add(NewRow(16, Inline("L16", "04", 0)));
        }
        WorksheetPart pnl = workbookPart.AddNewPart<WorksheetPart>();
        pnl.Worksheet = new Worksheet(new SheetData(rows));
        workbookPart.Workbook.AppendChild(new Sheets(new Sheet { Name = "PNL", SheetId = 1, Id = workbookPart.GetIdOfPart(pnl) }));
    }

    private static Row NewRow(uint index, params Cell[] cells) => new(cells) { RowIndex = index };

    private static Cell Inline(string reference, string value, uint style) => new()
    {
        CellReference = reference, DataType = CellValues.InlineString, StyleIndex = style,
        InlineString = new InlineString(new Text(value))
    };

    private static Cell? FindCell(SpreadsheetDocument document, string reference)
    {
        Sheet sheet = document.WorkbookPart!.Workbook!.Sheets!.Elements<Sheet>().Single(item => item.Name == "PNL");
        var part = (WorksheetPart)document.WorkbookPart.GetPartById(sheet.Id!.Value!);
        return part.Worksheet!.Descendants<Cell>().FirstOrDefault(cell => cell.CellReference == reference);
    }

    private static string CellText(SpreadsheetDocument document, string reference)
    {
        Cell? cell = FindCell(document, reference);
        if (cell is null) return string.Empty;
        if (cell.DataType?.Value == CellValues.SharedString)
        {
            return document.WorkbookPart!.SharedStringTablePart!.SharedStringTable!
                .Elements<SharedStringItem>().ElementAt(int.Parse(cell.CellValue!.Text)).InnerText;
        }
        return cell.InlineString?.InnerText ?? cell.CellValue?.Text ?? string.Empty;
    }

    private static bool Struck(SpreadsheetDocument document, string reference)
    {
        Stylesheet styles = document.WorkbookPart!.WorkbookStylesPart!.Stylesheet!;
        uint index = FindCell(document, reference)?.StyleIndex?.Value ?? 0;
        CellFormat format = styles.CellFormats!.Elements<CellFormat>().ElementAt((int)index);
        return styles.Fonts!.Elements<Font>().ElementAt((int)(format.FontId?.Value ?? 0)).GetFirstChild<Strike>() is not null;
    }

    private static T Required<T>(OperationResponse<T> response) =>
        response.Success && response.Data is not null ? response.Data : throw new InvalidOperationException(response.Message);

    private static void RequireFailure(OperationResponse response, string code)
    {
        if (response.Success || !response.Message.Contains(code, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected {code}, got '{response.Message}'.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
