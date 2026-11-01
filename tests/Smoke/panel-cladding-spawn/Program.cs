using System.Reflection;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingTestFixtures;

namespace PanelCladdingSpawnSmoke;

internal static class Program
{
    private static void Main()
    {
        var keys = new PanelCladdingKeyService();
        var planner = new PanelCladdingSpawnPlanningService(keys);
        var panelText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CW_2.03_OFFSET_H0"] = "50",
            ["CW_2.04_OFFSET_V0"] = "50",
            ["CW_1.01_PID"] = " W3-06-42 ",
            ["CW_1.05_LOT"] = " R7 ",
            ["CW_1.05_RELEASE"] = "OLD-IGNORED",
            ["CW_1.07_WALL_TYPE"] = " WT-03 ",
            ["PID"] = "IGNORED_PID",
            ["Release"] = "IGNORED_RELEASE",
            ["WallType"] = "IGNORED_WALL_TYPE",
            [PanelCladdingKeyService.GetCellKey(0, "A")] = " gl01 ",
            [PanelCladdingKeyService.GetCellKey(0, "B")] = "GL01",
            [PanelCladdingKeyService.GetCellKey(1, "A")] = "stn02",
            [PanelCladdingKeyService.GetCellKey(1, "B")] = string.Empty
        };

        PanelCladdingKeySet keySet = RequireData(keys.Parse(CladdingLogicFixture.WithLogic(panelText), 100d, 100d, 0.001d), "Parse grid");
        PanelCladdingSpawnPlan plan = RequireData(planner.CreatePlan(panelText, keySet), "Create spawn plan");
        Require(plan.PanelId == "W3-06-42", "PID was not normalized for CID authority.");
        Require(plan.Regions.Count == 3, "Blank cladding cells must not create surfaces.");
        Require(plan.Regions.Select(region => region.Cid).SequenceEqual(new[]
            { "W3-06-42_0A", "W3-06-42_0B", "W3-06-42_1A" }),
            "CID ordering/naming is not column-first and bottom-to-top.");
        Require(PanelCladdingSpawnPlanningService.BuildSurfaceCid(
                "PID-ABC-W3-05-25", "0A") == "CID-ABC-W3-05-25_0A",
            "A semantic PID- prefix must become CID- for a spawned surface.");
        Require(PanelCladdingSpawnPlanningService.BuildSurfaceCid(
                "W3-06-42", "0A") == "W3-06-42_0A",
            "A plain panel identifier must preserve the legacy CID naming behavior.");

        PanelCladdingSpawnRegionPlan glass = plan.Regions[0];
        Require(glass.LayerPath == "Reference::Surfaces - STEP::Surfaces - GL::GL01",
            "GL01 did not route to the required glass material layer.");
        Require(plan.Regions[1].LayerPath == glass.LayerPath,
            "Cells using the same material must reuse one layer path.");
        Require(glass.UserTextWrites["CW_1.02_CID"] == "W3-06-42_0A",
            "Canonical CID user text is missing.");
        Require(!glass.UserTextWrites.ContainsKey("CID"),
            "The noncanonical CID alias must not be written.");
        Require(glass.UserTextWrites["CW_1.01_PID"] == "W3-06-42", "PID source key/value was not inherited.");
        Require(glass.UserTextWrites["CW_1.05_LOT"] == "R7", "Canonical lot key/value was not inherited.");
        Require(!glass.UserTextWrites.ContainsKey("CW_1.05_RELEASE"), "Former release key was propagated.");
        Require(glass.UserTextWrites["CW_1.07_WALL_TYPE"] == "WT-03", "Canonical wall type key/value was not inherited.");
        Require(!glass.UserTextWrites.ContainsKey("PID") &&
                !glass.UserTextWrites.ContainsKey("Release") &&
                !glass.UserTextWrites.ContainsKey("WallType"),
            "Noncanonical metadata aliases must be ignored.");
        Require(glass.UserTextWrites["Cladding"] == "GL01", "Normalized cladding information is missing.");
        Require(plan.Regions[2].LayerPath == "Reference::Surfaces - STEP::Surfaces - STN::STN02",
            "STN02 did not route to its code-prefix material layer.");
        Console.WriteLine("[OK] PID-derived CIDs, metadata inheritance, blank-cell handling, and material layers");

        Require(PanelCladdingSpawnPlanningService.ResolveMaterialFamilyLayer("TER01") == "Surfaces - TER",
            "Terracotta routing failed.");
        Require(PanelCladdingSpawnPlanningService.ResolveMaterialFamilyLayer("ACP01") == "Surfaces - ACP",
            "Metal routing failed.");
        Require(PanelCladdingSpawnPlanningService.ResolveMaterialFamilyLayer("01") == "Surfaces - Other",
            "Codes without a letter prefix must use the explicit fallback family.");

        PanelColorRgb glass01 = PanelCladdingSpawnPlanningService.ResolveMaterialLayerColor("GL01");
        PanelColorRgb glass02 = PanelCladdingSpawnPlanningService.ResolveMaterialLayerColor("GL02");
        Require(glass01 == new PanelColorRgb(0x59, 0x83, 0x90),
            "GL01 must use the calibrated #598390 layer color.");
        Require(glass02 == new PanelColorRgb(0x46, 0x68, 0x73),
            "GL02 must use the calibrated #466873 layer color.");
        Require(ColorDistance(glass01, glass02) >= 60,
            "GL01 and GL02 need a clearly visible within-family color difference.");
        Require(glass01 != new PanelColorRgb(255, 255, 255) &&
                glass02 != new PanelColorRgb(255, 255, 255),
            "Spawn material layers must not use the default white color.");
        Require(glass01.Blue > glass01.Red && glass01.Green > glass01.Red &&
                glass02.Blue > glass02.Red && glass02.Green > glass02.Red,
            "Glass colors must retain a cool glass-like hue.");
        Require(glass01 == PanelCladdingSpawnPlanningService.ResolveMaterialLayerColor("gl01"),
            "Material layer colors must be deterministic and case-insensitive.");
        Require(glass.LayerColor == glass01 && plan.Regions[1].LayerColor == glass01,
            "Cells on the same material layer must share its deterministic color.");
        PanelColorRgb stone01 = PanelCladdingSpawnPlanningService.ResolveMaterialLayerColor("STN01");
        PanelColorRgb stone02 = PanelCladdingSpawnPlanningService.ResolveMaterialLayerColor("STN02");
        Require(ColorDistance(stone01, stone02) >= 60,
            "Non-glass material families also need a clearly visible variant difference.");
        Console.WriteLine("[OK] deterministic material-family routing, fallback, and stronger related color variants");

        var missingPid = new Dictionary<string, string>(panelText, StringComparer.OrdinalIgnoreCase);
        missingPid.Remove("CW_1.01_PID");
        OperationResponse<PanelCladdingSpawnPlan> missingPidResult = planner.CreatePlan(missingPid, keySet);
        Require(!missingPidResult.Success && missingPidResult.Message.Contains("PID_REQUIRED", StringComparison.Ordinal),
            "Missing PID must prevent spawning.");

        var missingRelease = new Dictionary<string, string>(panelText, StringComparer.OrdinalIgnoreCase);
        missingRelease.Remove("CW_1.05_LOT");
        OperationResponse<PanelCladdingSpawnPlan> missingReleaseResult = planner.CreatePlan(missingRelease, keySet);
        Require(!missingReleaseResult.Success &&
            missingReleaseResult.Message.Contains("LOT_NUMBER_REQUIRED", StringComparison.Ordinal) &&
            missingReleaseResult.Message.Contains("CW_1.05_LOT", StringComparison.Ordinal),
            "The former release key or alias must not replace the missing canonical lot key.");

        var missingWallType = new Dictionary<string, string>(panelText, StringComparer.OrdinalIgnoreCase);
        missingWallType.Remove("CW_1.07_WALL_TYPE");
        OperationResponse<PanelCladdingSpawnPlan> missingWallTypeResult = planner.CreatePlan(missingWallType, keySet);
        Require(!missingWallTypeResult.Success &&
            missingWallTypeResult.Message.Contains("WALL_TYPE_REQUIRED", StringComparison.Ordinal) &&
            missingWallTypeResult.Message.Contains("CW_1.07_WALL_TYPE", StringComparison.Ordinal),
            "A WallType alias must not replace the missing canonical Wall Type key.");

        var invalidMaterial = new Dictionary<string, string>(panelText, StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetCellKey(0, "A")] = "GL::01"
        };
        PanelCladdingKeySet invalidKeySet = RequireData(keys.Parse(CladdingLogicFixture.WithLogic(invalidMaterial), 100d, 100d, 0.001d), "Parse invalid layer fixture");
        OperationResponse<PanelCladdingSpawnPlan> invalidMaterialResult = planner.CreatePlan(invalidMaterial, invalidKeySet);
        Require(!invalidMaterialResult.Success &&
            invalidMaterialResult.Message.Contains("MATERIAL_LAYER_NAME_INVALID", StringComparison.Ordinal),
            "Invalid Rhino layer segments must prevent spawning.");
        Console.WriteLine("[OK] canonical metadata is required, aliases are ignored, and invalid layer names fail closed");

        VerifyCommandContract();
        Console.WriteLine("[OK] PCbake generates through the live spawn adapter; command GUIDs are unique");
    }

    // Since 1.3.0 PCbake is the only command that generates cladding geometry. The live spawn
    // adapter remains its per-panel generator and no longer has a batch entry of its own.
    private static void VerifyCommandContract()
    {
        Assembly assembly = typeof(PanelCladdingSpawnPlanningService).Assembly;
        Type bakeCommand = assembly.GetType("PanelCladdingEditor.UI.PanelCladdingBakeCommand") ??
            throw new InvalidOperationException("PanelCladdingBake command is missing.");
        Type editorCommand = assembly.GetType("PanelCladdingEditor.UI.PanelCladdingEditorCommand") ??
            throw new InvalidOperationException("PanelCladdingEditor command is missing.");
        Require(bakeCommand.GUID != Guid.Empty, "Bake command GUID must be explicit and non-empty.");
        Require(bakeCommand.GUID != editorCommand.GUID, "Rhino command GUIDs must be unique.");
        Type generator = assembly.GetType("PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding.LivePanelCladdingSpawnService") ??
            throw new InvalidOperationException("Live spawn adapter is missing.");
        Require(generator.GetMethod("Spawn") is null &&
                assembly.GetType("PanelCladdingEditor.Application.Interfaces.ILivePanelCladdingSpawnService") is null,
            "The batch spawn entry point must be gone.");

        MethodInfo prepare = generator.GetMethod("PreparePanel", BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException("Live per-panel generation contract is missing.");
        ParameterInfo[] parameters = prepare.GetParameters();
        Require(parameters.Length == 5 && parameters[2].ParameterType == typeof(Guid) &&
                parameters[3].ParameterType == typeof(PanelCladdingObjectScope),
            "Live generation must take one panel object ID and an object-family scope.");

        string[] forbidden = { "MCP_Rhino", "ModelContextProtocol", "Microsoft.Extensions.Hosting" };
        Require(!assembly.GetReferencedAssemblies().Any(reference => forbidden.Any(token =>
                (reference.Name ?? string.Empty).Contains(token, StringComparison.OrdinalIgnoreCase))),
            "Spawn capability introduced an MCP_Rhino or MCP SDK dependency.");
    }

    private static T RequireData<T>(OperationResponse<T> response, string operation)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operation} failed: {response.Message}");
        }
        return response.Data;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static int ColorDistance(PanelColorRgb left, PanelColorRgb right)
    {
        return Math.Abs(left.Red - right.Red) +
            Math.Abs(left.Green - right.Green) +
            Math.Abs(left.Blue - right.Blue);
    }
}
