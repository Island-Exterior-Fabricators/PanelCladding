using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingUdKeysSmoke;

internal static class Program
{
    private const string LegacyLogic = "CW_9.01_CLADDING_LOGIC";
    private const string LegacyFrame = "CW_9.02_FRAME_CONFIG";
    private const string Config = "1H 1V | delete A1";
    private const string Logic = "{\"0A\":\"0A\"}";

    private static void Main()
    {
        VerifyKeyNames();
        VerifyLegacyPanelReads();
        VerifyMigrationPlan();
        VerifyLegacyKeyRecognition();
        Console.WriteLine("PASS: panel cladding UD_ key regression");
    }

    private static void VerifyKeyNames()
    {
        Require(PanelCladdingKeyService.CladdingLogicKey == "UD_CLADDING_LOGIC" &&
                PanelCladdingKeyService.FrameConfigBackupKey == "UD_FRAME_CONFIG",
            "The plug-in writes UD_CLADDING_LOGIC and UD_FRAME_CONFIG.");
        Require(PanelCladdingKeyService.LegacyUserDefinedCladdingLogicKey == LegacyLogic &&
                PanelCladdingKeyService.LegacyUserDefinedFrameConfigKey == LegacyFrame &&
                PanelCladdingKeyService.LegacyUserDefinedKey("ud_frame_config") == LegacyFrame &&
                PanelCladdingKeyService.LegacyUserDefinedKey(PanelCladdingKeyService.FrameConfigKey) is null,
            "Each UD_ key names its CW_9.xx predecessor; other keys have none.");
        IReadOnlyDictionary<string, string> writes = Required(new PanelCladdingKeyService().EncodeFrameConfigurationUserText(
            new PanelCladdingTopologyState { MissingSegments = [H(0, 1)] }, 1, 1));
        Require(writes.Count == 1 && writes[PanelCladdingKeyService.FrameConfigBackupKey] == Config,
            "Planners write the readable configuration to UD_FRAME_CONFIG.");
        Console.WriteLine("[OK] UD_CLADDING_LOGIC / UD_FRAME_CONFIG replace CW_9.01 / CW_9.02");
    }

    // A panel written by 1.1.0-1.2.1 carries only CW_9.xx keys and must still read.
    private static void VerifyLegacyPanelReads()
    {
        var service = new PanelCladdingKeyService();
        var topology = new PanelCladdingTopologyState { MissingSegments = [H(0, 1)] };
        var legacy = new Dictionary<string, string>
        {
            ["CW_2.03_OFFSET_H1"] = "30", ["CW_2.04_OFFSET_V1"] = "40",
            [PanelCladdingKeyService.FrameConfigKey] = "AA", [LegacyFrame] = Config, [LegacyLogic] = Logic
        };
        Require(Same(Required(service.DecodeTopology(legacy, 1, 1)), topology), "A legacy coded panel decodes from CW_9.02.");
        Require(Required(service.ReadCanonicalFrameConfiguration(legacy)) == Config, "The canonical read accepts CW_9.02.");
        Require(PanelCladdingKeyService.ValidateFrameAttributeNames(legacy).Success, "Legacy frame keys validate.");
        Require(PanelCladdingKeyService.ReadUserDefinedValue(legacy, PanelCladdingKeyService.FrameConfigBackupKey) == Config &&
                PanelCladdingKeyService.ReadUserDefinedValue(legacy, PanelCladdingKeyService.CladdingLogicKey) == Logic,
            "UD_ reads fall back to the CW_9.xx value.");

        var mixed = new Dictionary<string, string>(legacy)
        {
            [PanelCladdingKeyService.FrameConfigBackupKey] = "1H 1V", [PanelCladdingKeyService.CladdingLogicKey] = "{}"
        };
        Require(PanelCladdingKeyService.ReadUserDefinedValue(mixed, PanelCladdingKeyService.FrameConfigBackupKey) == "1H 1V" &&
                PanelCladdingKeyService.ReadUserDefinedValue(mixed, PanelCladdingKeyService.CladdingLogicKey) == "{}" &&
                Same(Required(service.DecodeTopology(mixed, 1, 1)), new PanelCladdingTopologyState()),
            "A UD_ value wins over a leftover CW_9.xx value.");
        var blank = new Dictionary<string, string>(legacy) { [PanelCladdingKeyService.FrameConfigBackupKey] = " " };
        Require(Same(Required(service.DecodeTopology(blank, 1, 1)), topology), "A blank UD_FRAME_CONFIG falls back to CW_9.02.");
        RequireFailure(service.DecodeTopology(new Dictionary<string, string> { [PanelCladdingKeyService.FrameConfigKey] = "AA" }, 1, 1),
            "PANEL_FRAME_CONFIG_BACKUP_MISSING");
        Console.WriteLine("[OK] legacy CW_9.xx panels decode, validate and read; UD_ wins when both exist");
    }

    private static void VerifyMigrationPlan()
    {
        var legacy = new Dictionary<string, string>
        {
            [LegacyFrame] = Config, ["cw_9.01_cladding_logic"] = Logic,
            ["UD_OFFSET_CUSTOM"] = "keep", ["CW_9.99_OFFSET_CUSTOM"] = "keep"
        };
        var (deletes, writes) = PanelCladdingKeyService.PlanUserDefinedKeyMigration(legacy);
        Require(deletes.OrderBy(key => key, StringComparer.Ordinal).SequenceEqual(new[] { LegacyFrame, "cw_9.01_cladding_logic" }) &&
                writes.Count == 2 &&
                writes[PanelCladdingKeyService.FrameConfigBackupKey] == Config &&
                writes[PanelCladdingKeyService.CladdingLogicKey] == Logic,
            "Legacy keys (any case) are renamed to their UD_ keys; other keys are left alone.");

        var rewritten = new Dictionary<string, string>
        {
            [LegacyFrame] = Config, [LegacyLogic] = Logic,
            [PanelCladdingKeyService.FrameConfigBackupKey] = "1H 1V", [PanelCladdingKeyService.CladdingLogicKey] = "{}"
        };
        (deletes, writes) = PanelCladdingKeyService.PlanUserDefinedKeyMigration(rewritten);
        Require(deletes.Count == 2 && writes.Count == 0, "A UD_ value written by the same operation wins; the legacy key is only deleted.");

        (deletes, writes) = PanelCladdingKeyService.PlanUserDefinedKeyMigration(new Dictionary<string, string>
        {
            [PanelCladdingKeyService.FrameConfigBackupKey] = Config, ["UD_OFFSET_CUSTOM"] = "keep"
        });
        Require(deletes.Count == 0 && writes.Count == 0, "A migrated panel needs no change.");

        (deletes, writes) = PanelCladdingKeyService.PlanUserDefinedKeyMigration(new Dictionary<string, string> { [LegacyLogic] = " " });
        Require(deletes.Count == 1 && writes.Count == 0, "A blank legacy key is dropped without writing an empty UD_ key.");
        Console.WriteLine("[OK] migration renames CW_9.01/CW_9.02, keeps a newer UD_ value and leaves unrelated keys");
    }

    private static void VerifyLegacyKeyRecognition()
    {
        var service = new PanelCladdingKeyService();
        Require(PanelCladdingKeyService.IsTopologyKey(LegacyFrame) && service.IsClearableCladdingAssignmentKey(LegacyLogic) &&
                service.IsClearableCladdingAssignmentKey(LegacyFrame) && !service.IsClearableCladdingAssignmentKey("UD_OFFSET_CUSTOM"),
            "Topology and clear paths remove the legacy keys but not unrelated UD_ keys.");

        var writes = new Dictionary<string, string> { [PanelCladdingKeyService.FrameConfigBackupKey] = Config };
        Require(PanelCladdingKeyService.TopologyPersistenceMatches(new Dictionary<string, string>(writes), writes),
            "A migrated panel with the expected configuration matches.");
        Require(!PanelCladdingKeyService.TopologyPersistenceMatches(new Dictionary<string, string> { [LegacyFrame] = Config }, writes) &&
                !PanelCladdingKeyService.TopologyPersistenceMatches(
                    new Dictionary<string, string>(writes) { [LegacyFrame] = Config }, writes),
            "A stored CW_9.02 is out of date, so PCSyncSrf rewrites (and renames) the panel.");

        PanelCladdingClearPlan plan = Required(new PanelCladdingClearPlanningService(service).CreatePlan(
        [
            new PanelCladdingClearPanelSnapshot
            {
                ObjectId = Guid.NewGuid(),
                UserText = new Dictionary<string, string>
                {
                    [LegacyLogic] = Logic, [LegacyFrame] = Config, ["UD_OFFSET_CUSTOM"] = "keep", ["CW_1.01_PID"] = "keep"
                }
            }
        ]));
        Require(plan.Panels.Single().UserTextDeletes.OrderBy(key => key, StringComparer.Ordinal).SequenceEqual(new[] { LegacyLogic, LegacyFrame }),
            "PCClear removes legacy cladding logic and frame configuration only.");
        Console.WriteLine("[OK] legacy keys are topology/clearable keys and out of date for persistence checks");
    }

    private static PanelCladdingSegmentCoordinate H(int track, int bay) => new(PanelCladdingTopologyAxis.Horizontal, track, bay);

    private static bool Same(PanelCladdingTopologyState actual, PanelCladdingTopologyState expected) =>
        actual.MissingSegments.ToHashSet().SetEquals(expected.MissingSegments) &&
        actual.HiddenSegments.ToHashSet().SetEquals(expected.HiddenSegments) &&
        actual.MergeRuns.ToHashSet().SetEquals(expected.MergeRuns);

    private static T Required<T>(OperationResponse<T> response)
    {
        Require(response.Success && response.Data is not null, response.Message);
        return response.Data!;
    }

    private static void RequireFailure<T>(OperationResponse<T> response, string prefix) =>
        Require(!response.Success && response.Message.StartsWith(prefix, StringComparison.Ordinal),
            $"Expected {prefix}, got '{response.Message}'.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
