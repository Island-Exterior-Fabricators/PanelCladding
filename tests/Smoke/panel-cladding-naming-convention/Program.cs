using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using PanelCladdingEditor.Application.Services;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;

namespace PanelCladdingNamingConventionSmoke;

internal static partial class Program
{
    private const string Grey = "FF808080";

    private static void Main()
    {
        VerifyReadableFrameConfig();
        VerifyLegacyFrameConfigConversion();
        VerifyNamingFormat();
        VerifyFrameConfigCodes();
        VerifyFrameConfigCodeFormats();
        VerifyUnitTypeRoles();
        VerifyRoleDefinitionReservation();
        VerifyCodeTables();
        VerifyWorkbook();
        VerifyTemplate();
        VerifyMainModel();
        Console.WriteLine("PASS: panel cladding naming convention regression");
    }

    // Only "<code> - Wireframe.3dm" writes the sheet on save; other models of the project read it.
    private static void VerifyMainModel()
    {
        foreach (string path in new[]
                 {
                     @"C:\Projects\ABC\Wireframe\ABC - Wireframe.3dm",
                     @"C:\Projects\XYZ\Wireframe\XYZ - Wireframe.3dm",
                     "xyz - wireframe.3DM", "P00001 - Wireframe.3dm", " ABC - Wireframe.3dm "
                 })
        {
            Require(PanelCladdingProjectService.IsMainModelPath(path), "Main model rejected: " + path);
        }
        foreach (string? path in new[]
                 {
                     @"C:\Projects\XYZ\Wireframe\XYZ - Wireframe_STN.3dm",
                     "ABC - Wireframe - v2.3dm", "ABC - Wireframe (1).3dm", "ABC - Wireframe copy.3dm", "ABC_old - Wireframe.3dm",
                     "abc-wireframe.3dm", "QRS_Wireframe.3dm", "ABC - STN.3dm", "ABC.3dm", "ABC - Wireframe.3dmbak",
                     "ABC - Wireframe.rhl", "ABC - Wireframe", "RhinoAutosave.3dm", " - Wireframe.3dm", "", " ", null
                 })
        {
            Require(!PanelCladdingProjectService.IsMainModelPath(path), "Not a main model: " + path);
        }
        Require(PanelCladdingProjectService.MainModelFileName(" xyz ") == "XYZ - Wireframe.3dm", "Main model file name text.");
        Console.WriteLine("[OK] only <PROJECT> - Wireframe.3dm syncs the sheet; XYZ - Wireframe_STN.3dm and copies do not");
    }

    private static void VerifyReadableFrameConfig()
    {
        var topology = new PanelCladdingTopologyState
        {
            MissingSegments = [H(0, 1), V(0, 1)],
            MergeRuns = [new PanelCladdingMergeRun(PanelCladdingTopologyAxis.Horizontal, 1, 0, 1)],
            HiddenSegments = [V(0, 0)]
        };
        string text = Required(new PanelCladdingKeyService().EncodeFrameConfiguration(topology, 2, 1));
        Require(text == "2H 1V | delete A1 0B | merge B0–1 | hide 0A", "Readable frame config text: " + text);
        Require(PanelFrameConfigFormat.Format(new PanelCladdingTopologyState(), 0, 0) == "0H 0V",
            "A panel without intermediates is '0H 0V'.");

        PanelFrameConfigFormat.Parsed parsed = Required(PanelFrameConfigFormat.Parse(text));
        Require(parsed.HorizontalTrackCount == 2 && parsed.VerticalTrackCount == 1 &&
                Same(parsed.Topology, topology), "Readable frame config must round-trip losslessly.");
        Require(Same(Required(PanelFrameConfigFormat.Parse("2H 1V | merge B0-1")).Topology,
                new PanelCladdingTopologyState { MergeRuns = topology.MergeRuns }),
            "A hyphen is accepted in merge ranges.");
        foreach (string invalid in new[] { "A0", "2H 1V | delete", "2H 1V | delete A1 | delete 0B", "2H 1V | split A1", "TBD" })
        {
            OperationResponse<PanelFrameConfigFormat.Parsed> rejected = PanelFrameConfigFormat.Parse(invalid);
            Require(!rejected.Success && rejected.Message.StartsWith("PANEL_FRAME_CONFIG_TEXT_INVALID", StringComparison.Ordinal),
                $"'{invalid}' must be rejected.");
        }

        IReadOnlyDictionary<string, string> writes = Required(new PanelCladdingKeyService().EncodeFrameConfigurationUserText(topology, 2, 1));
        Require(writes.Count == 1 && writes[PanelCladdingKeyService.FrameConfigBackupKey] == text,
            "Planners write the readable configuration to UD_FRAME_CONFIG only.");
        Require(PanelCladdingKeyService.TopologyPersistenceMatches(
                new Dictionary<string, string> { [PanelCladdingKeyService.FrameConfigBackupKey] = text, [PanelCladdingKeyService.FrameConfigKey] = "AA" },
                writes),
            "The derived CW_1.08 code does not count as a topology mismatch.");
        Console.WriteLine("[OK] UD_FRAME_CONFIG holds a readable lossless frame config: " + text);
    }

    private static void VerifyLegacyFrameConfigConversion()
    {
        var service = new PanelCladdingKeyService();
        var topology = new PanelCladdingTopologyState { MissingSegments = [H(0, 1)] };
        PanelCladdingTopologyPayloads payloads = Required(service.EncodeTopology(topology, 1, 1));
        string json = JsonSerializer.Serialize(new { v = 1, delete = payloads.SegmentMask, merge = payloads.MergeMask, hide = payloads.HideMask });
        var legacy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CW_2.03_OFFSET_H1"] = "30", ["CW_2.04_OFFSET_V1"] = "40", [PanelCladdingKeyService.FrameConfigKey] = json
        };
        Require(Required(service.ReadCanonicalFrameConfiguration(legacy)) == "1H 1V | delete A1",
            "Former JSON configurations convert to the readable form.");
        Require(Same(Required(service.DecodeTopology(legacy, 1, 1)), topology), "Former JSON configurations still decode.");
        Require(Required(service.ReadCanonicalFrameConfiguration(new Dictionary<string, string>
                { [PanelCladdingKeyService.FrameConfigKey] = "TBD" })) == string.Empty,
            "Placeholder text is not a frame configuration.");

        var coded = new Dictionary<string, string>
        {
            [PanelCladdingKeyService.FrameConfigKey] = "AA", [PanelCladdingKeyService.FrameConfigBackupKey] = "1H 1V | delete A1"
        };
        Require(Same(Required(service.DecodeTopology(coded, 1, 1)), topology), "A coded panel decodes from its UD_FRAME_CONFIG backup.");
        RequireFailure(service.DecodeTopology(coded, 2, 1), "PANEL_FRAME_CONFIG_GRID_MISMATCH");
        RequireFailure(service.DecodeTopology(new Dictionary<string, string> { [PanelCladdingKeyService.FrameConfigKey] = "AA" }, 1, 1),
            "PANEL_FRAME_CONFIG_BACKUP_MISSING");
        Require(service.IsClearableCladdingAssignmentKey(PanelCladdingKeyService.LegacyCladdingLogicKey) &&
                PanelCladdingKeyService.CladdingLogicKey == "UD_CLADDING_LOGIC",
            "Cladding logic moved to UD_CLADDING_LOGIC (1.2.2); the retired CW_2.13 key is cleaned up.");
        Console.WriteLine("[OK] JSON configs convert, codes read from UD_FRAME_CONFIG, cladding logic is UD_CLADDING_LOGIC");
    }

    private static void VerifyNamingFormat()
    {
        Require(string.Concat(PanelNamingFormat.Sequence("A")) == "ABCDEFGHJKLMNPQRSTUVWXYZ", "One-letter codes skip I and O.");
        string[] two = PanelNamingFormat.Sequence("01").ToArray();
        Require(two[0] == "01" && two[98] == "99" && two[99] == "A1" && two[107] == "A9" && two[108] == "B1" &&
                two[99 + 216] == "AA" && two.Length == 99 + 216 + 576 && two.Distinct().Count() == two.Length,
            "Two-digit codes continue 01..99, then A1..Z9, then AA..ZZ.");
        string[] three = PanelNamingFormat.Sequence("001").ToArray();
        Require(three[0] == "001" && three[^1] == "999" && three.Length == 999, "Three-digit codes run 001..999.");
        string[] letters = PanelNamingFormat.Sequence("AA").ToArray();
        Require(letters[0] == "AA" && letters[1] == "AB" && letters.Length == 576 && !letters.Any(code => code.Contains('I') || code.Contains('O')),
            "Two-letter codes run AA..ZZ without I/O.");
        Require(PanelNamingFormat.Sequence("A1").First() == "A1" && PanelNamingFormat.Sequence("A1").Count() == 216 + 576,
            "Letter+digit codes run A1..Z9, then AA..ZZ.");
        Require(PanelNamingFormat.Fits("A", "A") && !PanelNamingFormat.Fits("A", "I") && !PanelNamingFormat.Fits("A", "AA") &&
                PanelNamingFormat.Fits("01", "10") && !PanelNamingFormat.Fits("01", "00") && PanelNamingFormat.Fits("01", "A1") &&
                PanelNamingFormat.Fits("01", "ZZ") && !PanelNamingFormat.Fits("01", "001") && PanelNamingFormat.Fits("001", "035") &&
                !PanelNamingFormat.Fits("001", "35") && PanelNamingFormat.Fits("A1", "B9") && !PanelNamingFormat.Fits("A1", "B0") &&
                PanelNamingFormat.Fits("AA", "AB") && !PanelNamingFormat.Fits("AA", "A1") && !PanelNamingFormat.Fits("AA", "ab"),
            "Codes fit their format, including the two-character overflow.");
        Require(PanelNamingFormat.LooksLikeCode("A") && PanelNamingFormat.LooksLikeCode("AA") && !PanelNamingFormat.LooksLikeCode("TBD") &&
                !PanelNamingFormat.LooksLikeCode(" ") && !PanelNamingFormat.LooksLikeCode("5") && !PanelNamingFormat.LooksLikeCode(null),
            "Any format's code shape is recognised; placeholder text is not.");
        Require(PanelNamingFormat.ReadToken(" a1 ") == "A1" && PanelNamingFormat.ReadToken("aa") == "AA" && PanelNamingFormat.ReadToken("001") == "001" &&
                PanelNamingFormat.ReadToken("1") is null && PanelNamingFormat.ReadToken("AAA") is null && PanelNamingFormat.ReadToken("0001") is null &&
                PanelNamingFormat.ReadToken("B") is null,
            "Only AA, A, A1, 01 and 001 are accepted answers.");
        var met = new PanelNamingFormat("A", "A", "01", "001", "01");
        Require(met.Serialize() == "Wall-Type=A; Frame-Config=A; Frame-Type=01; Cladding=001; Definition=01" &&
                Required(PanelNamingFormat.Parse(met.Serialize())) == met &&
                Required(PanelNamingFormat.Parse("wall type = A; frame config=A; FRAME-TYPE=01; cladding=001")) == met,
            "The format text round-trips; Definition defaults to 01.");
        foreach (string invalid in new[] { "Wall-Type=B; Frame-Config=A; Frame-Type=01; Cladding=001", "Wall-Type=A",
                     "Wall-Type=A; Wall-Type=A; Frame-Config=A; Frame-Type=01; Cladding=001", "Seq=0001; Wall-Type=A; Frame-Config=A; Frame-Type=01; Cladding=001" })
        {
            RequireFailure(PanelNamingFormat.Parse(invalid), "PANEL_NAMING_FORMAT_INVALID");
        }
        Require(PanelNamingFormat.Legacy == new PanelNamingFormat("A", "AA", "01", "01", "01"), "The legacy format is ABC's.");
        RequireFailure(new PanelCladdingKeyService().DecodeTopology(new Dictionary<string, string> { [PanelCladdingKeyService.FrameConfigKey] = "A" }, 0, 0),
            "PANEL_FRAME_CONFIG_BACKUP_MISSING");
        Console.WriteLine("[OK] PNL format tokens, sequences (I/O skipped, 01..99 -> A1 -> AA) and format text");
    }

    private static void VerifyFrameConfigCodeFormats()
    {
        var single = new PanelFrameConfigCodeRegistry("A");
        Require(single.Register("A", "0H 0V").Success && !single.Register("AA", "1H 0V").Success,
            "A one-letter registry accepts A and rejects AA.");
        Require(Resolve(single, "1H 0V", expectAssigned: true) == "B" && Resolve(single, "1H 1V", expectAssigned: true) == "C" &&
                Resolve(single, "0H 0V", expectAssigned: false) == "A",
            "One-letter codes take the next free letter.");
        var digits = new PanelFrameConfigCodeRegistry("01");
        var threeDigits = new PanelFrameConfigCodeRegistry("001");
        Require(Resolve(digits, "0H 0V", expectAssigned: true) == "01" && Resolve(digits, "1H 0V", expectAssigned: true) == "02" &&
                Resolve(threeDigits, "0H 0V", expectAssigned: true) == "001",
            "Digit codes count up.");
        var grouped = new PanelFrameConfigCodeRegistry("A1");
        Require(Resolve(grouped, "1H 1V", expectAssigned: true) == "A1" && Resolve(grouped, "1H 1V | delete A0", expectAssigned: true) == "A2" &&
                Resolve(grouped, "2H 0V", expectAssigned: true) == "B1",
            "Letter+digit codes group by intermediate counts like AA.");
        PanelFrameConfigCodeRegistry reloaded = Required(PanelFrameConfigCodeRegistry.FromJson(
            "[{\"Code\":\"AA\",\"Config\":\"0H 0V\"},{\"Code\":\"B\",\"Config\":\"1H 0V\"}]", "A"));
        Require(reloaded.Entries.Count == 1 && reloaded.Entries["B"] == "1H 0V" && Resolve(reloaded, "0H 0V", expectAssigned: true) == "A",
            "Stored codes of another format are skipped, not carried into this one.");
        var full = new PanelFrameConfigCodeRegistry("A");
        for (int count = 0; count < 24; count++) Resolve(full, $"{count}H 0V", expectAssigned: true);
        RequireFailure(full.Resolve("24H 0V", out _), "PANEL_FRAME_CONFIG_CODES_EXHAUSTED");
        Console.WriteLine("[OK] frame config codes follow the recorded format (A, 01, 001 in order; AA, A1 grouped)");
    }

    private static void VerifyFrameConfigCodes()
    {
        var registry = new PanelFrameConfigCodeRegistry();
        string[] sameClass =
        [
            "1H 1V", "1H 1V | delete A0", "1H 1V | delete A1", "1H 1V | delete 0A", "1H 1V | delete 0B",
            "1H 1V | hide A0", "1H 1V | hide A1", "1H 1V | hide 0A", "1H 1V | hide 0B"
        ];
        string[] codes = sameClass.Select(config => Resolve(registry, config, expectAssigned: true)).ToArray();
        Require(codes.SequenceEqual(new[] { "AA", "AB", "AC", "AD", "AE", "AF", "AG", "AH", "AJ" }),
            "Same intermediate counts share the first letter and skip I: " + string.Join(", ", codes));
        Require(Resolve(registry, "2H 1V", expectAssigned: true) == "BA" &&
                Resolve(registry, "0H 0V", expectAssigned: true) == "CA" &&
                Resolve(registry, "2H 1V | merge B0–1", expectAssigned: true) == "BB" &&
                Resolve(registry, "1H 1V", expectAssigned: false) == "AA",
            "A new class takes the next first letter; known configs keep their code.");

        PanelFrameConfigCodeRegistry reloaded = Required(PanelFrameConfigCodeRegistry.FromJson(registry.ToJson()));
        Require(reloaded.Entries.Count == registry.Entries.Count &&
                reloaded.Entries.All(entry => registry.Entries[entry.Key] == entry.Value) &&
                Resolve(reloaded, "1H 1V | merge A0–1", expectAssigned: true) == "AK",
            "Codes persist through the document string and are never reused.");
        Require(!reloaded.Register("AA", "2H 0V").Success && !reloaded.Register("ZZ", "1H 1V").Success &&
                reloaded.Register("AA", "1H 1V").Success,
            "Registering a conflicting pair is rejected.");
        Require(registry.Token == "AA" && registry.IsCode("AZ") && !registry.IsCode("AI") && !registry.IsCode("OA") &&
                !registry.IsCode("A") && !registry.IsCode("aa"),
            "Without a recorded format codes are two capital letters without I and O.");
        Console.WriteLine("[OK] frame config codes AA, AB, ... group by intermediate counts and are stable");
    }

    private static void VerifyUnitTypeRoles()
    {
        const string pid = "PID-ABC-N2-05-05";
        (string UnitType, string? Role, string Cid)[] cases =
        [
            ("F", "Parent", "CID-ABC-N2-05-05"),
            ("C", "Parent", "CID-ABC-N2-05-05"),
            ("L", "Parent", "CID-ABC-N2-05-05_P"),
            ("L", "Child", "CID-ABC-N2-05-05_C"),
            ("Z", "Parent", "CID-ABC-N2-05-05_P"),
            ("Z", "Child 1", "CID-ABC-N2-05-05_C1"),
            ("Z", "child 2", "CID-ABC-N2-05-05_C2")
        ];
        foreach ((string unitType, string? role, string cid) in cases)
        {
            Require(PanelCladdingCidService.ResolvePanelCid(pid, Text(unitType, role)) == cid,
                $"{unitType}/{role} must resolve to {cid}.");
        }
        Require(PanelCladdingCidService.ResolvePanelCid(pid, Text("F", "Parent", "CID-ABC-N2-05-05_P")) == "CID-ABC-N2-05-05",
            "Flat panels drop a stale role suffix.");
        Require(PanelCladdingCidService.ResolvePanelCid(pid, Text("L", null, "CID-ABC-N2-05-05_C")) == "CID-ABC-N2-05-05_C" &&
                PanelCladdingCidService.PanelCidWrite(Text("L", null)) is null &&
                PanelCladdingCidService.PanelCidWrite(Text("corner_child", null)) is null,
            "Without a role (or with a retired unit type) the stored CID is not rewritten.");
        Require(PanelCladdingCidService.SurfaceCid(pid, "0A", Text("L", "Child")) == "CID-ABC-N2-05-05_C_0A" &&
                PanelCladdingCidService.SurfaceCid(pid, "0A", Text("F", "Parent")) == "CID-ABC-N2-05-05_0A",
            "Surface CIDs follow the panel role.");
        Console.WriteLine("[OK] C/F carry no role; L/Z use _P, _C, _C1, _C2 from CW_5.00_DEFINITION_A");
    }

    private static void VerifyRoleDefinitionReservation()
    {
        var flat = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PanelCladdingCidService.UnitTypeUserTextKey] = "F",
            [Definition('A')] = "Glass", [Definition('B')] = "Anchor", [Definition('C')] = " ", [Definition('D')] = "Note"
        };
        PanelCladdingPidSetupService.ReserveRoleDefinition(flat, new Dictionary<string, string>());
        Require(flat[Definition('A')] == "parent" && flat[Definition('B')] == "Glass" && flat[Definition('C')] == "Anchor" &&
                flat[Definition('D')] == " " && flat[Definition('E')] == "Note",
            "A taken Definition A moves the definitions down and adds E: " + Dump(flat));
        var again = new Dictionary<string, string>(flat, StringComparer.Ordinal);
        PanelCladdingPidSetupService.ReserveRoleDefinition(again, new Dictionary<string, string>());
        Require(again.Count == flat.Count && again.All(item => flat[item.Key] == item.Value), "Reserving the role is idempotent.");

        var child = new Dictionary<string, string>(StringComparer.Ordinal);
        PanelCladdingPidSetupService.ReserveRoleDefinition(child, new Dictionary<string, string>
        {
            [PanelCladdingCidService.UnitTypeUserTextKey] = "L", [Definition('A')] = "Child", [Definition('B')] = "Kept"
        });
        Require(child[Definition('A')] == "child" && !child.ContainsKey(Definition('B')) && !child.ContainsKey(Definition('E')),
            "An existing role stays in Definition A, lowercase; an unchanged existing definition is not rewritten.");

        var unknown = new Dictionary<string, string>(StringComparer.Ordinal);
        PanelCladdingPidSetupService.ReserveRoleDefinition(unknown, new Dictionary<string, string>
        {
            [PanelCladdingCidService.UnitTypeUserTextKey] = "L", [Definition('A')] = "Glass"
        });
        Require(unknown[Definition('A')] == " " && unknown[Definition('B')] == "Glass",
            "A multi-face panel without a role keeps Definition A blank for it.");

        var formerChild = new Dictionary<string, string>(StringComparer.Ordinal) { [PanelCladdingCidService.UnitTypeUserTextKey] = "C" };
        PanelCladdingPidSetupService.ReserveRoleDefinition(formerChild, new Dictionary<string, string> { [Definition('A')] = "Child" });
        Require(formerChild[Definition('A')] == "parent", "Flat and curved panels are always parent.");
        Console.WriteLine("[OK] CW_5.00_DEFINITION_A is reserved for Parent/Child; flat panels are Parent");
    }

    private static void VerifyCodeTables()
    {
        string[] sequence = PanelNamingFormat.Sequence(PanelNamingFormat.Legacy.WallType).ToArray();
        Require(sequence.Length == 24 && !sequence.Contains("I") && !sequence.Contains("O") && sequence[23] == "Z",
            "Legacy wall-type codes run A..Z without I/O.");

        PanelNamingConventionTables tables = PanelNamingConventionService.BuildTables(
            new Dictionary<string, string> { ["BA"] = "2H 0V", ["AA"] = "1H 1V", ["AB"] = "1H 1V | delete A0" },
            new PanelNamingConventionSheetContent
            {
                FrameConfigs = new Dictionary<string, string> { ["AA"] = "A0", ["AC"] = "1H 1V | hide 0A", ["AF"] = "B2" },
                WallTypes = new Dictionary<string, string> { ["B"] = "WT-03", ["A"] = "WT-01" }
            },
            new PanelNamingConventionUsage
            {
                UnitTypes = new HashSet<string> { "F", "L" },
                FrameConfigCodes = new HashSet<string> { "AA", "BA" },
                WallTypes = new HashSet<string> { "WT-10", "WT-01", "WT-02" }
            });
        Require(Rows(tables.UnitTypes) == "C:Curved:- F:Flat:+ L:Two Face:+ Z:Three Face:-",
            "Unit types: " + Rows(tables.UnitTypes));
        Require(Rows(tables.FrameConfigs) == "AA:1H 1V:+ AB:1H 1V | delete A0:- AC:1H 1V | hide 0A:- BA:2H 0V:+",
            "Frame configs keep model codes, reserve formatted sheet rows and do not list the rest: " + Rows(tables.FrameConfigs));
        Require(Rows(tables.WallTypes) == "A:WT-01:+ B:WT-03:- C:WT-02:+ D:WT-10:+",
            "Wall types keep sheet codes and number new values naturally: " + Rows(tables.WallTypes));
        PanelNamingConventionTables numbered = PanelNamingConventionService.BuildTables(
            new Dictionary<string, string>(),
            new PanelNamingConventionSheetContent { WallTypes = new Dictionary<string, string> { ["01"] = "1A.1", ["03"] = "1A.4" } },
            new PanelNamingConventionUsage { WallTypes = new HashSet<string> { "1A.1", "1C.1" } },
            format: new PanelNamingFormat("01", "A", "01", "001", "01"));
        Require(Rows(numbered.WallTypes) == "01:1A.1:+ 02:1C.1:+ 03:1A.4:-",
            "A new wall value takes the first free code of the wall format: " + Rows(numbered.WallTypes));
        var full = new Dictionary<string, string>();
        foreach (string code in PanelNamingFormat.Sequence("A")) full[code] = "WT-" + code;
        PanelNamingConventionTables exhausted = PanelNamingConventionService.BuildTables(new Dictionary<string, string>(),
            new PanelNamingConventionSheetContent { WallTypes = full },
            new PanelNamingConventionUsage { WallTypes = new HashSet<string> { "WT-NEW" } }, format: new PanelNamingFormat("A", "A", "01", "001", "01"));
        Require(exhausted.UncodedWallTypes.SequenceEqual(new[] { "WT-NEW" }) && exhausted.WallTypes.Count == 24,
            "A wall value without a free one-letter code is reported, not given a wider code.");
        Require(PanelNamingConventionService.JobCodeFits("ABC", 3) && PanelNamingConventionService.JobCodeFits("ABC", 1) &&
                !PanelNamingConventionService.JobCodeFits("ABCD", 3) && !PanelNamingConventionService.JobCodeFits("", 3),
            "The project code fills the Job Code cells one each, or one cell whole.");
        Console.WriteLine("[OK] PNL code tables mark unused rows and never reuse codes");
    }

    private static void VerifyWorkbook()
    {
        string directory = Path.Combine(Path.GetTempPath(), "PanelCladdingNamingConvention-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "ABC - Naming Convention.xlsx");
        try
        {
            CreateWorkbook(path);
            var workbook = new OpenXmlPanelNamingConventionWorkbook();
            PanelNamingConventionSheetContent sheet = Required(workbook.Read(path));
            Require(sheet.FrameConfigs.Count == 3 && sheet.FrameConfigs["AC"] == "1H 1V | hide 0A" && sheet.FrameConfigs["AD"] == "A1" &&
                    !sheet.FrameConfigs.ContainsKey("AB") && sheet.WallTypes.Count == 2 && sheet.WallTypes["C"] == "5" &&
                    sheet.JobCodeWidth == 3 && sheet.RecordedFormat == string.Empty,
                "Read returns the rows with a value under the titles (shared strings, numbers), the Job Code width and no format.");

            PanelNamingConventionTables tables = PanelNamingConventionService.BuildTables(
                new Dictionary<string, string> { ["AA"] = "1H 1V", ["AB"] = "1H 1V | delete A0" }, sheet,
                Usage(["F", "L"], ["AA"], ["WT-01", "WT-02", "5"]), " abc ");
            Require(tables.JobCode == "ABC", "The job code is the trimmed upper-case project code.");
            string stale = Path.Combine(directory, ".ABC - Naming Convention.naming.0123456789abcdef.bak.tmp.xlsx");
            File.WriteAllText(stale, "left by an earlier save");
            File.SetCreationTimeUtc(stale, DateTime.UtcNow.AddHours(-1));
            File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddHours(-1));
            Require(workbook.Write(path, tables).Success, "First write must succeed.");
            Require(!File.Exists(stale), "A temp or backup file left by an earlier save is removed.");
            (int Fonts, int Formats) firstStyleCount = StyleCount(path);

            using (SpreadsheetDocument document = SpreadsheetDocument.Open(path, false))
            {
                Func<string, string> text = reference => CellText(document, "PNL", reference);
                Require(text("B2") == "Panel" && text("I7") == "Wall-Type" && text("J7") == "Frame-Config" && text("I8") == "A" &&
                        text("I11") == "*letter I,O not used", "Rows 1-11 are kept.");
                Require(Row(text, 12) == "Unit-Type|Description|Wall-Type|Value|Frame-Config|Configuration" &&
                        StyleOf(document, "E12") == 1 && StyleOf(document, "J12") == 1, "Row 12 titles are kept as they are.");
                Require(Row(text, 13) == "C|Curved|A|WT-01|AA|1H 1V" && Row(text, 14) == "F|Flat|B|WT-02|AB|1H 1V | delete A0" &&
                        Row(text, 15) == "L|Two Face|C|5|AC|1H 1V | hide 0A" && Row(text, 16) == "Z|Three Face|||AD|A1" &&
                        text("J20") == "junk",
                    "Codes keep their rows, placeholders are filled in place and other rows stay.");
                Require(FindCell(document, "PNL", "I15")?.DataType is null && FindCell(document, "PNL", "I15")?.CellValue?.Text == "5",
                    "A number in a managed column stays a number.");
                Require(text("N12") == "Cladding" && text("N13") == "1" && text("O13") == "GFRC-01" && text("R13") == "Seq note",
                    "Columns outside the three tables are left alone.");
                string[] merges = Merges(document);
                Require(merges.Contains("B2:V3") && merges.Contains("J7:K7") && merges.Contains("R13:U16") && merges.Contains("J20:K21"),
                    "Merges are left alone: " + string.Join(" ", merges));
                Require(!Struck(document, "J13") && !Struck(document, "E14") && !Struck(document, "H13") && !Struck(document, "I14") &&
                        !Struck(document, "H15") && Struck(document, "E13") && Struck(document, "F13") && Struck(document, "J14") &&
                        Struck(document, "K14") && Struck(document, "K15") && Struck(document, "E16") && !Struck(document, "J16"),
                    "Unused codes are struck through in grey; used codes and rows outside the tables are plain.");
                Require(StyleOf(document, "J13") == 2 && StyleOf(document, "K13") == 0,
                    "Rows keep their own style.");
                Require(!Bold(document, "E14"), "A table without rows takes the title style without bold.");
                Require(Row8(text) == "A|B|C" && StyleOf(document, "D8") == 2 && StyleOf(document, "F8") == 2,
                    "The Job Code cells under the merged header get the project code, one letter each, styles kept.");
                Require(CellText(document, "Other", "A1") == "keep", "Other sheets are untouched.");
                string[] errors = new OpenXmlValidator().Validate(document).Select(error => $"{error.Path?.XPath}: {error.Description}").ToArray();
                Require(errors.Length == 0, "The written workbook must be schema-valid: " + string.Join("; ", errors));
            }

            byte[] firstBytes = File.ReadAllBytes(path);
            OperationResponse unchanged = workbook.Write(path, tables);
            Require(unchanged.Success && unchanged.Message == OpenXmlPanelNamingConventionWorkbook.UnchangedMessage &&
                    File.ReadAllBytes(path).SequenceEqual(firstBytes),
                "Saving again with the same tables leaves the workbook untouched.");
            PanelNamingConventionTables Tables(string jobCode, params string[] walls) => PanelNamingConventionService.BuildTables(
                new Dictionary<string, string> { ["AA"] = "1H 1V", ["AB"] = "1H 1V | delete A0" },
                Required(workbook.Read(path)), Usage(["F", "L"], ["AA"], walls), jobCode);
            Require(workbook.Write(path, Tables("QRS", "WT-01", "WT-02", "5")).Message != OpenXmlPanelNamingConventionWorkbook.UnchangedMessage &&
                    JobCodeOf(path) == "Q|R|S", "A different project code rewrites the Job Code.");
            Require(workbook.Write(path, Tables("ABCD", "WT-01", "WT-02", "5")).Message == OpenXmlPanelNamingConventionWorkbook.UnchangedMessage &&
                    JobCodeOf(path) == "Q|R|S", "A code that does not fit leaves the Job Code alone.");
            Require(workbook.Write(path, tables).Success && JobCodeOf(path) == "A|B|C", "The project code is restored.");
            Require(workbook.Write(path, Tables("ABC", "WT-01", "WT-02", "WT-03")).Success && StyleCount(path) == firstStyleCount,
                "Rewriting reuses the struck styles.");
            using (SpreadsheetDocument document = SpreadsheetDocument.Open(path, false))
            {
                Require(CellText(document, "PNL", "H16") == "D" && CellText(document, "PNL", "I16") == "WT-03" && Struck(document, "I15") &&
                        FindCell(document, "PNL", "I15")?.CellValue?.Text == "5",
                    "A new wall type gets the next code below the table; an unused number stays a number.");
            }
            PanelNamingConventionTables curvedUsed = PanelNamingConventionService.BuildTables(
                new Dictionary<string, string> { ["AA"] = "1H 1V", ["AB"] = "1H 1V | delete A0" },
                Required(workbook.Read(path)), Usage(["C", "F", "L"], ["AA", "AB"], ["WT-01"]), "ABC");
            Require(workbook.Write(path, curvedUsed).Success, "Third write must succeed.");
            using (SpreadsheetDocument document = SpreadsheetDocument.Open(path, false))
            {
                Require(!Struck(document, "E13") && StyleOf(document, "E13") == StyleOf(document, "E14") && !Struck(document, "J14") &&
                        Struck(document, "I14") && CellText(document, "PNL", "K15") == "1H 1V | hide 0A",
                    "A code that becomes used again turns plain; the reserved sheet-only row stays.");
            }
            PanelNamingConventionTables conflicting = PanelNamingConventionService.BuildTables(
                new Dictionary<string, string> { ["AA"] = "2H 2V", ["AB"] = "1H 1V | delete A0" },
                Required(workbook.Read(path)), Usage(["C", "F", "L"], ["AA", "AB"], ["WT-01"]), "ABC");
            Require(workbook.Write(path, conflicting).Message == OpenXmlPanelNamingConventionWorkbook.UnchangedMessage,
                "A written description is never replaced by a different model value.");
            using (SpreadsheetDocument document = SpreadsheetDocument.Open(path, false))
            {
                Require(CellText(document, "PNL", "K13") == "1H 1V", "The sheet keeps AA = 1H 1V.");
            }

            byte[] before = File.ReadAllBytes(path);
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                OperationResponse locked = workbook.Write(path, Tables("ABC", "WT-01", "WT-02", "WT-04"));
                Require(!locked.Success && locked.Message.StartsWith("PANEL_NAMING_CONVENTION_LOCKED", StringComparison.Ordinal),
                    "A locked sheet reports PANEL_NAMING_CONVENTION_LOCKED: " + locked.Message);
            }
            Require(File.ReadAllBytes(path).SequenceEqual(before), "A locked sheet is left unchanged.");
            Require(Directory.GetFiles(directory).Select(Path.GetFileName).SequenceEqual(new[] { "ABC - Naming Convention.xlsx" }),
                "No temporary or backup files are left: " + string.Join(", ", Directory.GetFiles(directory).Select(Path.GetFileName)));
            RequireFailure(workbook.Read(Path.Combine(directory, "missing.xlsx")), "PANEL_NAMING_CONVENTION_READ_FAILED");

            VerifyTitleLayouts(directory, workbook, tables);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
        Console.WriteLine("[OK] PNL tables are reconciled in place: codes keep their rows, placeholders filled, numbers kept, unused rows struck grey");
    }

    private static void VerifyTitleLayouts(string directory, OpenXmlPanelNamingConventionWorkbook workbook, PanelNamingConventionTables tables)
    {
        string moved = Path.Combine(directory, "moved.xlsx");
        CreateTitleWorkbook(moved, ("E12", "Unit-Type"), ("F12", "Description"), ("T12", " wall type "), ("U12", "Value"),
            ("J12", "FRAME CONFIG"), ("K12", "Configuration"));
        Require(workbook.Write(moved, tables).Success, "Write with moved titles.");
        using (SpreadsheetDocument document = SpreadsheetDocument.Open(moved, false))
        {
            Require(CellText(document, "PNL", "T13") == "A" && CellText(document, "PNL", "U14") == "WT-02" &&
                    CellText(document, "PNL", "H13") == string.Empty && CellText(document, "PNL", "J13") == "AA",
                "Titles are matched ignoring case, spaces and hyphens, and their table follows them.");
            Require(CellText(document, "PNL", "D8") == "ABC" && CellText(document, "PNL", "E8") == string.Empty,
                "A single-cell Job Code header takes the whole project code.");
        }

        string untitled = Path.Combine(directory, "untitled.xlsx");
        CreateTitleWorkbook(untitled);
        Require(workbook.Write(untitled, tables).Success, "Write without titles.");
        using (SpreadsheetDocument document = SpreadsheetDocument.Open(untitled, false))
        {
            Func<string, string> text = reference => CellText(document, "PNL", reference);
            Require(Row(text, 12) == "Unit-Type|Description|Wall-Type|Value|Frame-Config|Configuration" &&
                    Row(text, 13) == "C|Curved|A|WT-01|AA|1H 1V",
                "Missing titles are added in E, H and J.");
        }
        Require(workbook.Write(untitled, tables).Message == OpenXmlPanelNamingConventionWorkbook.UnchangedMessage,
            "Added titles make the next save a no-op.");

        string overlapping = Path.Combine(directory, "overlapping.xlsx");
        CreateTitleWorkbook(overlapping, ("E12", "Unit-Type"), ("F12", "Wall-Type"), ("J12", "Frame-Config"));
        RequireFailure(workbook.Read(overlapping), "PANEL_NAMING_CONVENTION_LAYOUT_INVALID");
        Require(workbook.Write(overlapping, tables).Message.StartsWith("PANEL_NAMING_CONVENTION_LAYOUT_INVALID", StringComparison.Ordinal),
            "Overlapping tables are rejected before writing.");
        string duplicate = Path.Combine(directory, "duplicate.xlsx");
        CreateTitleWorkbook(duplicate, ("J12", "Frame-Config"), ("R12", "Frame-Config"));
        RequireFailure(workbook.Read(duplicate), "PANEL_NAMING_CONVENTION_LAYOUT_INVALID");
        foreach (string file in new[] { moved, untitled, overlapping, duplicate }) File.Delete(file);
    }

    private static void CreateWorkbook(string path)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        WorkbookPart workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        AddStyles(workbookPart);
        var sharedStrings = workbookPart.AddNewPart<SharedStringTablePart>();
        sharedStrings.SharedStringTable = new SharedStringTable(
            new SharedStringItem(new Text("Panel")), new SharedStringItem(new Text("1H 1V | hide 0A")));

        WorksheetPart pnl = workbookPart.AddNewPart<WorksheetPart>();
        pnl.Worksheet = new Worksheet(
            new SheetData(
                NewRow(2, Shared("B2", 0, 1)),
                NewRow(7, Inline("D7", "Job Code", 1), Inline("H7", "Unit-Type", 1), Inline("I7", "Wall-Type", 1), Inline("J7", "Frame-Config", 1)),
                NewRow(8, Inline("D8", "R", 2), Inline("E8", "E", 2), Inline("F8", "G", 2), Inline("I8", "A", 0)),
                NewRow(11, Inline("I11", "*letter I,O not used", 0)),
                NewRow(12, Inline("E12", "Unit-Type", 1), Inline("F12", "Description", 1), Inline("H12", "Wall-Type", 1),
                    Inline("I12", "Value", 1), Inline("J12", "Frame-Config", 1), Inline("K12", "Configuration", 1),
                    Inline("N12", "Cladding", 1)),
                NewRow(13, Inline("H13", "A", 0), Inline("I13", "WT-01", 0), Inline("J13", "AA", 2), Inline("K13", "1H 1V", 0),
                    Inline("N13", "1", 0), Inline("O13", "GFRC-01", 0), Inline("R13", "Seq note", 0)),
                NewRow(14, Inline("H14", "B", 0), Inline("J14", "AB", 2)),
                NewRow(15, Inline("H15", "C", 0), Number("I15", 5, 0), Inline("J15", "AC", 2), Shared("K15", 1, 0)),
                NewRow(16, Inline("J16", "AD", 2), Inline("K16", "A1", 0)),
                NewRow(20, Inline("J20", "junk", 0))),
            new MergeCells(new MergeCell { Reference = "B2:V3" }, new MergeCell { Reference = "D7:F7" }, new MergeCell { Reference = "J7:K7" },
                new MergeCell { Reference = "R13:U16" }, new MergeCell { Reference = "J20:K21" }) { Count = 5 });
        WorksheetPart other = workbookPart.AddNewPart<WorksheetPart>();
        other.Worksheet = new Worksheet(new SheetData(NewRow(1, Inline("A1", "keep", 0))));
        workbookPart.Workbook.AppendChild(new Sheets(
            new Sheet { Name = "PNL", SheetId = 1, Id = workbookPart.GetIdOfPart(pnl) },
            new Sheet { Name = "Other", SheetId = 2, Id = workbookPart.GetIdOfPart(other) }));
    }

    private static void CreateTitleWorkbook(string path, params (string Reference, string Text)[] titles)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        WorkbookPart workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        AddStyles(workbookPart);
        WorksheetPart pnl = workbookPart.AddNewPart<WorksheetPart>();
        var data = new SheetData(NewRow(7, Inline("D7", "Job Code", 1), Inline("H7", "Unit-Type", 1)));
        if (titles.Length > 0) data.Append(NewRow(12, titles.Select(title => Inline(title.Reference, title.Text, 1)).ToArray()));
        pnl.Worksheet = new Worksheet(data);
        workbookPart.Workbook.AppendChild(new Sheets(new Sheet { Name = "PNL", SheetId = 1, Id = workbookPart.GetIdOfPart(pnl) }));
    }

    private static void AddStyles(WorkbookPart workbookPart)
    {
        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = new Stylesheet(
            new Fonts(new Font(new FontSize { Val = 11 }), new Font(new Bold(), new FontSize { Val = 11 })) { Count = 2 },
            new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 })) { Count = 2 },
            new Borders(new Border()) { Count = 1 },
            new CellFormats(
                new CellFormat(),
                new CellFormat { FontId = 1, ApplyFont = true },
                new CellFormat(new Alignment { Horizontal = HorizontalAlignmentValues.Center }) { ApplyAlignment = true })
            { Count = 3 });
    }

    private static Row NewRow(uint index, params Cell[] cells) => new(cells) { RowIndex = index };

    private static Cell Inline(string reference, string value, uint style) => new()
    {
        CellReference = reference, DataType = CellValues.InlineString, StyleIndex = style,
        InlineString = new InlineString(new Text(value))
    };

    private static Cell Number(string reference, int value, uint style) => new()
    {
        CellReference = reference, StyleIndex = style,
        CellValue = new CellValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture))
    };

    private static Cell Shared(string reference, int index, uint style) => new()
    {
        CellReference = reference, DataType = CellValues.SharedString, StyleIndex = style,
        CellValue = new CellValue(index.ToString(System.Globalization.CultureInfo.InvariantCulture))
    };

    private static Cell? FindCell(SpreadsheetDocument document, string sheetName, string reference)
    {
        Sheet sheet = document.WorkbookPart!.Workbook!.Sheets!.Elements<Sheet>().Single(item => item.Name == sheetName);
        var part = (WorksheetPart)document.WorkbookPart.GetPartById(sheet.Id!.Value!);
        return part.Worksheet!.Descendants<Cell>().FirstOrDefault(cell => cell.CellReference == reference);
    }

    private static string CellText(SpreadsheetDocument document, string sheetName, string reference)
    {
        Cell? cell = FindCell(document, sheetName, reference);
        if (cell is null) return string.Empty;
        if (cell.DataType?.Value == CellValues.SharedString)
        {
            return document.WorkbookPart!.SharedStringTablePart!.SharedStringTable!
                .Elements<SharedStringItem>().ElementAt(int.Parse(cell.CellValue!.Text)).InnerText;
        }
        return cell.InlineString?.InnerText ?? cell.CellValue?.Text ?? string.Empty;
    }

    private static uint StyleOf(SpreadsheetDocument document, string reference) =>
        FindCell(document, "PNL", reference)?.StyleIndex?.Value ?? 0;

    private static bool Struck(SpreadsheetDocument document, string reference)
    {
        Stylesheet styles = document.WorkbookPart!.WorkbookStylesPart!.Stylesheet!;
        CellFormat format = styles.CellFormats!.Elements<CellFormat>().ElementAt((int)StyleOf(document, reference));
        Font font = styles.Fonts!.Elements<Font>().ElementAt((int)(format.FontId?.Value ?? 0));
        bool struck = font.GetFirstChild<Strike>() is not null;
        Require(!struck || font.Elements<Color>().Any(color => color.Rgb?.Value == Grey), reference + " struck text must be grey.");
        return struck;
    }

    private static bool Bold(SpreadsheetDocument document, string reference)
    {
        Stylesheet styles = document.WorkbookPart!.WorkbookStylesPart!.Stylesheet!;
        CellFormat format = styles.CellFormats!.Elements<CellFormat>().ElementAt((int)StyleOf(document, reference));
        return styles.Fonts!.Elements<Font>().ElementAt((int)(format.FontId?.Value ?? 0)).GetFirstChild<Bold>() is not null;
    }

    private static (int Fonts, int Formats) StyleCount(string path)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(path, false);
        Stylesheet styles = document.WorkbookPart!.WorkbookStylesPart!.Stylesheet!;
        return (styles.Fonts!.Elements<Font>().Count(), styles.CellFormats!.Elements<CellFormat>().Count());
    }

    private static string[] Merges(SpreadsheetDocument document)
    {
        Sheet sheet = document.WorkbookPart!.Workbook!.Sheets!.Elements<Sheet>().Single(item => item.Name == "PNL");
        var part = (WorksheetPart)document.WorkbookPart.GetPartById(sheet.Id!.Value!);
        return part.Worksheet!.Descendants<MergeCell>().Select(merge => merge.Reference?.Value ?? string.Empty).ToArray();
    }

    private static string Row8(Func<string, string> text) => string.Join("|", new[] { "D", "E", "F" }.Select(column => text(column + "8")));

    private static string JobCodeOf(string path)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(path, false);
        return Row8(reference => CellText(document, "PNL", reference));
    }

    private static string Row(Func<string, string> text, int row) =>
        string.Join("|", new[] { "E", "F", "H", "I", "J", "K" }.Select(column => text(column + row)));

    private static PanelNamingConventionUsage Usage(string[] unitTypes, string[] frameCodes, string[] wallTypes) => new()
    {
        UnitTypes = unitTypes.ToHashSet(), FrameConfigCodes = frameCodes.ToHashSet(), WallTypes = wallTypes.ToHashSet()
    };

    private static string Rows(IEnumerable<PanelNamingCodeRow> rows) =>
        string.Join(" ", rows.Select(row => $"{row.Code}:{row.Description}:{(row.Used ? "+" : "-")}"));

    private static string Resolve(PanelFrameConfigCodeRegistry registry, string config, bool expectAssigned)
    {
        string code = Required(registry.Resolve(config, out bool assigned));
        Require(assigned == expectAssigned, $"'{config}' assigned={assigned}, expected {expectAssigned}.");
        return code;
    }

    private static string Definition(char letter) => "CW_5.00_DEFINITION_" + letter;

    private static IReadOnlyDictionary<string, string> Text(string unitType, string? role, string? cid = null)
    {
        var text = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingCidService.UnitTypeUserTextKey] = unitType,
            [PanelCladdingSpawnPlanningService.PanelIdUserTextKey] = "PID-ABC-N2-05-05"
        };
        if (role is not null) text[PanelCladdingCidService.RoleDefinitionKey] = role;
        if (cid is not null) text[PanelCladdingSpawnPlanningService.CidUserTextKey] = cid;
        return text;
    }

    private static PanelCladdingSegmentCoordinate H(int track, int bay) => new(PanelCladdingTopologyAxis.Horizontal, track, bay);

    private static PanelCladdingSegmentCoordinate V(int track, int bay) => new(PanelCladdingTopologyAxis.Vertical, track, bay);

    private static bool Same(PanelCladdingTopologyState actual, PanelCladdingTopologyState expected) =>
        actual.MissingSegments.ToHashSet().SetEquals(expected.MissingSegments) &&
        actual.HiddenSegments.ToHashSet().SetEquals(expected.HiddenSegments) &&
        actual.MergeRuns.ToHashSet().SetEquals(expected.MergeRuns);

    private static string Dump(IReadOnlyDictionary<string, string> values) =>
        string.Join(", ", values.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => $"{item.Key}='{item.Value}'"));

    private static T Required<T>(OperationResponse<T> response)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException(response.Message);
        }
        return response.Data;
    }

    private static void RequireFailure<T>(OperationResponse<T> response, string code) =>
        Require(!response.Success && response.Message.StartsWith(code, StringComparison.Ordinal),
            $"Expected {code}, got {(response.Success ? "success" : response.Message)}.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
