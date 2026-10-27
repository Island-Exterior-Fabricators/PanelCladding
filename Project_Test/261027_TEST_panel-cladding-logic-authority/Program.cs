using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingLogicAuthoritySmoke;

internal static class Program
{
    private const string Logic = PanelCladdingKeyService.CladdingLogicKey;
    // 1H 1V grid on a 30 x 40 panel: cells 0A, 0B, 1A, 1B.
    private static readonly Dictionary<string, string> Grid = new()
    {
        ["CW_2.03_OFFSET_H0"] = "20", ["CW_2.04_OFFSET_V0"] = "15"
    };

    private static void Main()
    {
        VerifyLogicFormat();
        VerifyUnconfiguredPanels();
        VerifyConfiguredPanels();
        VerifyCreatePlan();
        VerifyMatchSource();
        VerifySaveExtrusionsGuard();
        Console.WriteLine("PASS: panel cladding logic authority regression");
    }

    private static void VerifyLogicFormat()
    {
        var logic = new PanelCladdingLogicService();
        PanelCladdingCell[] cells = Cells(1, 1);
        string encoded = Required(logic.Encode(cells, new Dictionary<string, string>
        {
            [Key("0A")] = " al-01 ", [Key("0B")] = "0a", [Key("1A")] = "", [Key("1B")] = "A&B 2"
        }));
        Require(encoded == "{\"0A\":\"al-01\",\"0B\":\"0A\",\"1A\":\"\",\"1B\":\"A&B 2\"}",
            $"Owner cells store their material, members their owner label, unassigned cells \"\": {encoded}");
        IReadOnlyDictionary<string, string> values = Required(logic.DecodeValues(encoded));
        IReadOnlyDictionary<string, string> graph = Required(logic.Decode(encoded));
        Require(values["0A"] == "al-01" && values["0B"] == "0A" && values["1B"] == "A&B 2" &&
                graph["0A"] == "" && graph["0B"] == "0A" && graph["1B"] == "",
            "Values keep the materials; the ownership graph reads materials as roots.");
        Require(Required(logic.ResolveOwnerLabel(["0A", "0B"], graph)) == "0A",
            "Owner resolution still works on logic that carries materials.");
        Require(PanelCladdingLogicService.HasMaterials(values) &&
                !PanelCladdingLogicService.HasMaterials(Required(logic.DecodeValues("{\"0A\":\"\",\"0B\":\"0A\"}"))),
            "Ownership-only logic is recognized as written before 1.2.4.");
        RequireFailure(logic.Encode(cells, new Dictionary<string, string> { [Key("0A")] = "5Z" }),
            "PANEL_CLADDING_LOGIC_PARENT_UNKNOWN");
        RequireFailure(logic.DecodeValues("{\"0A\":\"0B\",\"0B\":\"0A\"}"), "PANEL_CLADDING_LOGIC_REFERENCE_CYCLE");
        Console.WriteLine("[OK] UD_CLADDING_LOGIC stores materials, parents and blanks");
    }

    // Unconfigured: only H/V decide; CW_4 keys are never read.
    private static void VerifyUnconfiguredPanels()
    {
        var keys = new PanelCladdingKeyService();
        var userText = new Dictionary<string, string>(Grid)
        {
            [Key("0A")] = "AL-01", [Key("0B")] = "7Q", [Key("5C")] = "ORPHAN", ["CW_4.09_CLADDING_9Z"] = "9Y"
        };
        PanelCladdingKeySet parsed = Required(keys.Parse(userText, 30d, 40d, 0.001d));
        Require(!parsed.CladdingConfigured && parsed.Cells.Count == 4 && parsed.Cells.All(cell => cell.Value.Length == 0),
            "Extra, missing and invalid CW_4 keys neither block nor fill an unconfigured panel.");
        Require(!PanelCladdingKeyService.IsCladdingConfigured(new Dictionary<string, string> { [Logic] = " " }),
            "A blank UD_CLADDING_LOGIC is unconfigured.");
        Console.WriteLine("[OK] unconfigured panels load from H/V alone");
    }

    private static void VerifyConfiguredPanels()
    {
        var keys = new PanelCladdingKeyService();
        var configured = new Dictionary<string, string>(Grid)
        {
            [Logic] = "{\"0A\":\"AL-01\",\"0B\":\"0A\",\"1A\":\"\",\"1B\":\"GL-02\"}",
            [Key("0A")] = "STALE", [Key("1A")] = "IGNORED"
        };
        PanelCladdingKeySet parsed = Required(keys.Parse(configured, 30d, 40d, 0.001d));
        Require(parsed.CladdingConfigured && Value(parsed, "0A") == "AL-01" && Value(parsed, "0B") == "0A" &&
                Value(parsed, "1A") == "" && Value(parsed, "1B") == "GL-02",
            "A configured panel reads its cladding from UD_CLADDING_LOGIC, not CW_4.");

        var mismatch = new Dictionary<string, string>(configured) { ["CW_2.04_OFFSET_V1"] = "25" };
        RequireFailure(keys.Parse(mismatch, 30d, 40d, 0.001d), "PANEL_CLADDING_LOGIC_GRID_MISMATCH");
        var shortLogic = new Dictionary<string, string>(Grid) { [Logic] = "{\"0A\":\"AL-01\"}" };
        RequireFailure(keys.Parse(shortLogic, 30d, 40d, 0.001d), "PANEL_CLADDING_LOGIC_GRID_MISMATCH");
        var badFrame = new Dictionary<string, string>(configured) { [PanelCladdingKeyService.FrameConfigBackupKey] = "2H 1V" };
        RequireFailure(keys.Parse(badFrame, 30d, 40d, 0.001d), "PANEL_FRAME_CONFIG_GRID_MISMATCH");

        var legacy = new Dictionary<string, string>(Grid)
        {
            [PanelCladdingKeyService.LegacyUserDefinedCladdingLogicKey] = "{\"0A\":\"\",\"0B\":\"0A\",\"1A\":\"\",\"1B\":\"\"}",
            [Key("0A")] = "AL-01", [Key("0B")] = "1B", [Key("1B")] = " "
        };
        PanelCladdingKeySet legacyParsed = Required(keys.Parse(legacy, 30d, 40d, 0.001d));
        Require(legacyParsed.CladdingConfigured && Value(legacyParsed, "0A") == "AL-01" &&
                Value(legacyParsed, "0B") == "0A" && Value(legacyParsed, "1B") == "",
            "Ownership-only logic takes root materials from CW_4 once; parents still come from the logic.");
        Console.WriteLine("[OK] configured panels need H/V, logic and frame configuration to agree");
    }

    private static void VerifyCreatePlan()
    {
        var planning = new PanelCladdingCreatePlanningService(new PanelCladdingKeyService());
        var userCladding = new Dictionary<string, string>
        {
            [Key("0A")] = "USER-TYPED", ["CW_4.07_CLADDING_7C"] = "EXTRA", ["CW_2.03_OFFSET_H3"] = "1",
            [PanelCladdingKeyService.LegacyCladdingLogicKey] = "{}", ["CW_2.13_CLADDING_TYPE"] = "OLD"
        };
        PanelCladdingCreatePanelPlan unconfigured = CreatePlan(planning, userCladding);
        Require(!unconfigured.UserTextWrites.Keys.Any(key => key.StartsWith("CW_4.", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals(Logic, StringComparison.OrdinalIgnoreCase)),
            "PCCreate writes no CW_4 placeholders and no cladding logic.");
        Require(!unconfigured.UserTextDeletes.Any(key => key.StartsWith("CW_4.", StringComparison.OrdinalIgnoreCase)) &&
                unconfigured.UserTextDeletes.Contains("CW_2.03_OFFSET_H3") &&
                unconfigured.UserTextDeletes.Contains(PanelCladdingKeyService.LegacyCladdingLogicKey) &&
                unconfigured.UserTextDeletes.Contains("CW_2.13_CLADDING_TYPE"),
            "PCCreate keeps the user's CW_4 keys and still clears offsets and retired keys.");
        Require(unconfigured.UserTextWrites.ContainsKey("CW_2.03_OFFSET_H0") &&
                unconfigured.UserTextWrites.ContainsKey("CW_2.04_OFFSET_V0") &&
                unconfigured.UserTextWrites.ContainsKey(PanelCladdingKeyService.FrameConfigBackupKey),
            "PCCreate still writes the grid and its frame configuration.");

        string fitting = "{\"0A\":\"AL-01\",\"0B\":\"0A\",\"1A\":\"\",\"1B\":\"\"}";
        PanelCladdingCreatePanelPlan kept = CreatePlan(planning, new Dictionary<string, string> { [Logic] = fitting });
        Require(!kept.UserTextDeletes.Contains(Logic) && !kept.UserTextWrites.ContainsKey(Logic),
            "Logic that fits the new grid is kept.");
        PanelCladdingCreatePanelPlan stale = CreatePlan(planning, new Dictionary<string, string>
        {
            ["ud_cladding_logic"] = "{\"0A\":\"AL-01\"}",
            [PanelCladdingKeyService.LegacyUserDefinedCladdingLogicKey] = "{\"0A\":\"\"}",
            [Key("0A")] = "AL-01"
        });
        Require(stale.UserTextDeletes.Contains("ud_cladding_logic") &&
                stale.UserTextDeletes.Contains(PanelCladdingKeyService.LegacyUserDefinedCladdingLogicKey) &&
                !stale.UserTextDeletes.Contains(Key("0A")),
            "Logic that no longer fits the grid is removed (UD_ and CW_9.01); CW_4 stays.");
        PanelCladdingCreatePanelPlan invalid = CreatePlan(planning, new Dictionary<string, string> { [Logic] = "not json" });
        Require(invalid.UserTextDeletes.Contains(Logic), "Unreadable logic is removed as stale.");
        Console.WriteLine("[OK] PCCreate owns the grid only and removes logic only when it no longer fits");
    }

    private static void VerifyMatchSource()
    {
        var planning = new PanelCladdingMatchPlanningService(new PanelCladdingKeyService());
        PanelCladdingMatchPanelSnapshot target = MatchPanel(new Dictionary<string, string>(Grid));
        var unconfigured = new Dictionary<string, string>(Grid) { [Key("0A")] = "AL-01" };
        RequireFailure(planning.CreatePlan(MatchPanel(unconfigured), [target]), "PANEL_CLADDING_MATCH_SOURCE_NOT_CONFIGURED");

        var configured = new Dictionary<string, string>(Grid)
        {
            [Logic] = "{\"0A\":\"AL-01\",\"0B\":\"0A\",\"1A\":\"GL-02\",\"1B\":\"1A\"}", [Key("0A")] = "STALE"
        };
        PanelCladdingMatchPlan plan = Required(planning.CreatePlan(MatchPanel(configured), [target]));
        IReadOnlyDictionary<string, string> writes = plan.Targets.Single().UserTextWrites;
        Require(writes[Key("0A")] == "AL-01" && writes[Key("1A")] == "GL-02" && writes[Key("0B")] == "0A" &&
                Required(new PanelCladdingLogicService().DecodeValues(writes[Logic]))["1A"] == "GL-02",
            "PCMatchSrf transfers the source logic, materials included, and writes CW_4 from it.");
        Console.WriteLine("[OK] PCMatchSrf needs a configured source and reads it from the logic");
    }

    private static void VerifySaveExtrusionsGuard()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingLayout configured = Layout(keys, new Dictionary<string, string>(Grid)
        {
            [Logic] = "{\"0A\":\"AL-01\",\"0B\":\"0A\",\"1A\":\"\",\"1B\":\"\"}"
        });
        RequireFailure(Save(keys, configured, [20d, 30d], PanelCladdingSaveScope.Extrusions),
            "PANEL_CLADDING_LOGIC_REQUIRES_SAVE_BOTH");
        Required(Save(keys, configured, [20d], PanelCladdingSaveScope.Extrusions));
        PanelCladdingLayout unconfigured = Layout(keys, new Dictionary<string, string>(Grid));
        Required(Save(keys, unconfigured, [20d, 30d], PanelCladdingSaveScope.Extrusions));
        Console.WriteLine("[OK] Save Extrusions cannot change the grid size of a configured panel");
    }

    private static OperationResponse<PanelCladdingSaveResult> Save(
        PanelCladdingKeyService keys,
        PanelCladdingLayout layout,
        double[] horizontal,
        PanelCladdingSaveScope scope) =>
        new PanelCladdingSaveService(new Repository(layout), new PanelCladdingTypeSignatureService(keys)).Save(
            new PanelCladdingSaveRequest
            {
                FilePath = layout.DocumentPath,
                ObjectId = layout.ObjectId,
                ExpectedGeometryFingerprint = layout.GeometryFingerprint,
                SystemCode = layout.SystemCode,
                HorizontalOffsets = horizontal,
                VerticalOffsets = layout.VerticalOffsets,
                Topology = new PanelCladdingTopologyState(),
                CellValues = layout.Cells.ToDictionary(cell => cell.UserTextKey, cell => cell.Value),
                Scope = scope
            });

    private static PanelCladdingLayout Layout(PanelCladdingKeyService keys, Dictionary<string, string> userText)
    {
        PanelCladdingKeySet keySet = Required(keys.Parse(userText, 30d, 40d, 0.001d));
        return new PanelCladdingLayout
        {
            ObjectId = Guid.NewGuid(),
            DocumentRuntimeSerialNumber = 1,
            DocumentPath = "C:/test/logic-authority.3dm",
            SystemCode = "WT01",
            GeometryFingerprint = "logic-authority",
            GeometryClass = PanelGeometryClass.Planar,
            Width = 30d,
            Height = 40d,
            ModelTolerance = 0.001d,
            HorizontalOffsets = keySet.HorizontalOffsets,
            VerticalOffsets = keySet.VerticalOffsets,
            Cells = keySet.Cells,
            Topology = keySet.Topology,
            SourceUserText = userText,
            Preview = new PanelPreviewGeometry()
        };
    }

    private static PanelCladdingCreatePanelPlan CreatePlan(
        PanelCladdingCreatePlanningService planning,
        Dictionary<string, string> userText) =>
        Required(planning.CreatePlan([new PanelCladdingCreatePanelSnapshot
        {
            ObjectId = Guid.NewGuid(),
            XMaximum = 30d, YMaximum = 40d, ZMinimum = -0.1d, ZMaximum = 0.1d, Tolerance = 0.001d,
            Guides =
            [
                Guide(new(0d, 20d, 0d), new(30d, 20d, 0d)),
                Guide(new(15d, 0d, 0d), new(15d, 40d, 0d))
            ],
            UserText = userText
        }])).Panels.Single();

    private static PanelCladdingCreateGuideSnapshot Guide(PanelPoint3 start, PanelPoint3 end) =>
        new() { ObjectId = Guid.NewGuid(), Samples = [start, end] };

    private static PanelCladdingMatchPanelSnapshot MatchPanel(Dictionary<string, string> userText) => new()
    {
        ObjectId = Guid.NewGuid(),
        SystemCode = "WT01",
        Geometry = new PanelCladdingMatchGeometryDescriptor
        {
            GeometryClass = PanelGeometryClass.Planar, Width = 30d, Height = 40d, ModelTolerance = 0.001d
        },
        UserText = userText
    };

    private static PanelCladdingCell[] Cells(int horizontal, int vertical) =>
        Required(new PanelCladdingKeyService().CreateKeySet(
            Enumerable.Range(1, horizontal).Select(index => 40d * index / (horizontal + 1)).ToArray(),
            Enumerable.Range(1, vertical).Select(index => 30d * index / (vertical + 1)).ToArray(),
            new Dictionary<string, string>(), 30d, 40d, 0.001d)).Cells.ToArray();

    private static string Key(string label) =>
        PanelCladdingKeyService.GetCellKey(int.Parse(label[..^1]), label[^1..]);

    private static string Value(PanelCladdingKeySet keySet, string label) =>
        keySet.Cells.Single(cell => cell.ShortLabel == label).Value;

    private static T Required<T>(OperationResponse<T> response)
    {
        Require(response.Success && response.Data is not null, response.Message);
        return response.Data!;
    }

    private static void RequireFailure<T>(OperationResponse<T> response, string prefix) =>
        Require(!response.Success && response.Message.StartsWith(prefix, StringComparison.Ordinal),
            $"Expected {prefix}, got {(response.Success ? "success" : response.Message)}.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class Repository(PanelCladdingLayout current) : ILivePanelCladdingRepository
    {
        public OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingLayout>.Ok(current);

        public OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail("Not used.");

        public OperationResponse<PanelAttributeCommitResult> CommitAttributes(
            PanelAttributeCommitRequest request,
            Func<OperationResponse> finalizeExternalCommit)
        {
            OperationResponse finalized = finalizeExternalCommit();
            return finalized.Success
                ? OperationResponse<PanelAttributeCommitResult>.Ok(new PanelAttributeCommitResult
                {
                    ObjectId = request.ObjectId,
                    Mutated = true
                })
                : OperationResponse<PanelAttributeCommitResult>.Fail(finalized.Message);
        }

        public OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath) =>
            OperationResponse<string>.Fail("Not used.");
    }
}
