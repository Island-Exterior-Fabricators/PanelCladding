using System.Reflection;
using System.Text.Json;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingTestFixtures;

namespace PanelCladdingLayerStandardSmoke;

internal static class Program
{
    private const string Root = "ABC-IEF-Wireframe";
    private const string PanelBranch = Root + "::Surfaces - PNL";
    private const string CurveBranch = Root + "::Curves - PNL";
    private const string StepRoot = "Reference::Surfaces - STEP";

    private static void Main()
    {
        Fixture fixture = JsonSerializer.Deserialize<Fixture>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "abc-layer-standard.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
            throw new InvalidOperationException("ABC layer fixture is missing.");

        VerifyProjectRoot();
        VerifyFileNameProject();
        VerifyPanelScope(fixture);
        VerifyCurveLayerRouting(fixture);
        VerifyManagedScopes(fixture);
        VerifyStepMaterialLayers(fixture);
        VerifySurfaceSyncLayerValidation(fixture);
        VerifyMaterialCategories();
        VerifyAbcSpawnPlan();
        VerifyNoLegacyLayerNamesInSource();
        Console.WriteLine("PASS: panel cladding layer standard regression");
    }

    private static void VerifyProjectRoot()
    {
        foreach (string name in new[] { Root, "QRS-IEF-Wireframe", "abc-ief-wireframe", "P00001-IEF-Wireframe" })
        {
            Require(PanelCladdingExtrusionPlanningService.IsProjectRootLayer(name), "Project root rejected: " + name);
        }
        foreach (string name in new[] { "", "-IEF-Wireframe", " -IEF-Wireframe", "IEF-Wireframe", "ABC-Wireframe",
                     "ABC-IEF-Wireframe-Old", "ABC-IEF-Wireframe::Surfaces - PNL", "01_CW Panels" })
        {
            Require(!PanelCladdingExtrusionPlanningService.IsProjectRootLayer(name), "Non-root accepted: " + name);
        }
        Require(PanelCladdingPidPlanningService.PanelLayerScope == "<PROJECT>-IEF-Wireframe::Surfaces - PNL",
            "PCpid scope text must describe the new panel branch.");
        Console.WriteLine("[OK] any <PROJECT>-IEF-Wireframe top-level layer is a project root");
    }

    // Since 1.1.3 the Rhino file name names the project: only <code>-IEF-Wireframe is its root.
    private static void VerifyFileNameProject()
    {
        (string Path, string Code)[] names =
        [
            (@"C:\Projects\ABC\Wireframe\ABC - Wireframe.3dm", "ABC"),
            ("abc-wireframe.3dm", "ABC"), ("QRS_Wireframe.3dm", "QRS"), ("JKL - Wireframe - v2.3dm", "JKL"),
            ("P00001 - Wireframe.3dm", "P00001"), ("", ""), (" ", "")
        ];
        foreach ((string path, string code) in names)
        {
            Require(PanelCladdingProjectService.CodeFromDocumentPath(path) == code, $"Project code of '{path}' must be '{code}'.");
        }
        Require(PanelCladdingProjectService.CodeFromDocumentPath(null) == string.Empty &&
                PanelCladdingProjectService.RootLayerName("abc") == Root &&
                PanelCladdingProjectService.PanelLayerScope("ABC") == PanelBranch &&
                PanelCladdingProjectService.PanelLayerScope("") == "<PROJECT>-IEF-Wireframe::Surfaces - PNL",
            "Root layer and scope text follow the file's project code.");

        string panel = PanelBranch + "::Surfaces - WT_01";
        string otherPanel = "QRS-IEF-Wireframe::Surfaces - PNL::Surfaces - WT_01";
        Require(PanelCladdingExtrusionPlanningService.IsProjectRootLayer("abc-ief-wireframe", "ABC") &&
                !PanelCladdingExtrusionPlanningService.IsProjectRootLayer("QRS-IEF-Wireframe", "ABC") &&
                PanelCladdingExtrusionPlanningService.IsProjectRootLayer("QRS-IEF-Wireframe", null),
            "With a file project only its root counts; without one any project root does.");
        Require(PanelCladdingPidPlanningService.IsPanelLayerPath(panel, "ABC") && !PanelCladdingPidPlanningService.IsPanelLayerPath(otherPanel, "ABC") &&
                PanelCladdingPidPlanningService.IsPanelLayerPath(otherPanel, "") &&
                PanelCladdingSpawnPlanningService.IsManagedExtrusionLayerPath(Root + "::Curves - PNL::Curves - WT_01", "ABC") &&
                !PanelCladdingSpawnPlanningService.IsManagedExtrusionLayerPath("QRS-IEF-Wireframe::Curves - PNL::Curves - WT_01", "ABC"),
            "Panel and managed-curve scopes follow the file's project.");
        Require(PanelCladdingExtrusionPlanningService.ResolveCurveLayerPath(panel, "ABC").Data == Root + "::Curves - PNL::Curves - WT_01",
            "Curves of a ABC panel go to ABC-IEF-Wireframe::Curves - PNL.");
        OperationResponse<string> foreign = PanelCladdingExtrusionPlanningService.ResolveCurveLayerPath(otherPanel, "ABC");
        Require(!foreign.Success && foreign.Message.Contains(PanelBranch + "::Surfaces - <type>", StringComparison.Ordinal),
            "A panel under another project's root is rejected with the expected ABC layer: " + foreign.Message);
        var keySet = new PanelCladdingKeySet { HorizontalOffsets = [], VerticalOffsets = [], Cells = Array.Empty<PanelCladdingCell>() };
        Require(!new PanelCladdingExtrusionPlanningService().CreatePlan("PID-ABC-N1-01-01", "CID-ABC-N1-01-01", 10, 10, keySet, otherPanel,
                    projectCode: "ABC").Success &&
                new PanelCladdingExtrusionPlanningService().CreatePlan("PID-ABC-N1-01-01", "CID-ABC-N1-01-01", 10, 10, keySet, panel,
                    projectCode: "ABC").Success,
            "Curve planning uses the file's project root.");
        Console.WriteLine("[OK] the Rhino file name names the project: ABC - Wireframe.3dm works under ABC-IEF-Wireframe only");
    }

    private static void VerifyPanelScope(Fixture fixture)
    {
        string[] panelLayers = fixture.Layers.Where(PanelCladdingPidPlanningService.IsPanelLayerPath).ToArray();
        Require(panelLayers.Length == 9 && panelLayers.All(path =>
                path == PanelBranch || path.StartsWith(PanelBranch + "::", StringComparison.Ordinal)),
            "ABC panel scope must be exactly Surfaces - PNL and its eight sublayers: " + string.Join(", ", panelLayers));
        foreach (string path in new[]
                 {
                     PanelBranch + "::Surfaces - WT_08 (SB)",
                     PanelBranch + "::Surfaces - Not in Scope",
                     "qrs-ief-wireframe::surfaces - pnl::Surfaces - WT_01",
                     "QRS-IEF-Wireframe::Surfaces - PNL::Tower A::Surfaces - WT_09"
                 })
        {
            Require(PanelCladdingPidPlanningService.IsPanelLayerPath(path), "Include panel scope: " + path);
        }
        foreach (string path in new[]
                 {
                     "", Root, Root + "::Surfaces - PNL-Old::Surfaces - WT_01", CurveBranch + "::Curves - WT_01",
                     Root + "::Surfaces - GLS::GLS-001", Root + "::Other::Surfaces - PNL",
                     "Other::" + PanelBranch, StepRoot + "::Surfaces - GLS::GLS-001",
                     "01_CW Panels::Surfaces-PNL::WT-01", "-IEF-Wireframe::Surfaces - PNL::Surfaces - WT_01"
                 })
        {
            Require(!PanelCladdingPidPlanningService.IsPanelLayerPath(path), "Exclude panel scope: " + path);
        }
        Console.WriteLine("[OK] panel scope is <PROJECT>-IEF-Wireframe::Surfaces - PNL and every nested sublayer");
    }

    private static void VerifyCurveLayerRouting(Fixture fixture)
    {
        Require(fixture.PanelCurveLayerPairs.Count == 6, "Fixture must contain the six live ABC panel/curve layer pairs.");
        foreach (LayerPair pair in fixture.PanelCurveLayerPairs)
        {
            string resolved = Required(PanelCladdingExtrusionPlanningService.ResolveCurveLayerPath(pair.PanelLayer), pair.PanelLayer);
            Require(resolved == pair.CurveLayer,
                $"{pair.PanelLayer} resolved to {resolved}; the live document hosts its {pair.PidCount} panels' curves on {pair.CurveLayer}.");
            Require(fixture.Layers.Contains(resolved), "Resolved curve layer must already exist in ABC: " + resolved);
        }

        (string Panel, string Curve)[] cases =
        {
            (PanelBranch + "::Surfaces - WT_08 (SB)", CurveBranch + "::Curves - WT_08 (SB)"),
            (PanelBranch + "::Surfaces - Not in Scope", CurveBranch + "::Curves - Not in Scope"),
            ("QRS-IEF-Wireframe::Surfaces - PNL::Surfaces - WT_02", "QRS-IEF-Wireframe::Curves - PNL::Curves - WT_02"),
            (PanelBranch + "::Surfaces - Tower A::Surfaces - WT_09", CurveBranch + "::Curves - Tower A::Curves - WT_09"),
            (PanelBranch + "::WT-04", CurveBranch + "::WT-04"),
            ("abc-ief-wireframe::surfaces - pnl::surfaces - WT_01", "abc-ief-wireframe::Curves - PNL::Curves - WT_01")
        };
        foreach ((string panel, string curve) in cases)
        {
            string resolved = Required(PanelCladdingExtrusionPlanningService.ResolveCurveLayerPath(panel), panel);
            Require(resolved == curve, $"{panel} resolved to {resolved}, expected {curve}.");
        }

        OperationResponse<string> branchOnly = PanelCladdingExtrusionPlanningService.ResolveCurveLayerPath(PanelBranch);
        Require(!branchOnly.Success && branchOnly.Message.StartsWith("PANEL_CLADDING_PANEL_TYPE_LAYER_REQUIRED", StringComparison.Ordinal),
            "A panel directly on Surfaces - PNL must not produce an untyped curve layer.");
        foreach (string unsupported in new[]
                 {
                     "01_CW Panels::Surfaces-PNL::WT-04", Root + "::Surfaces - GLS::GLS-001",
                     CurveBranch + "::Curves - WT_01", "Other::" + PanelBranch + "::Surfaces - WT_01"
                 })
        {
            OperationResponse<string> response = PanelCladdingExtrusionPlanningService.ResolveCurveLayerPath(unsupported);
            Require(!response.Success &&
                    response.Message.StartsWith("PANEL_CLADDING_PANEL_LAYER_NOT_SUPPORTED", StringComparison.Ordinal) &&
                    response.Message.Contains(PanelCladdingExtrusionPlanningService.PanelSurfaceLayerPattern, StringComparison.Ordinal),
                "Unsupported panel layer must fail with the expected pattern: " + unsupported);
        }
        Console.WriteLine("[OK] Surfaces - <type> panels route curves to Curves - <type> under their own project root");
    }

    private static void VerifyManagedScopes(Fixture fixture)
    {
        string[] curveLayers = fixture.Layers.Where(PanelCladdingSpawnPlanningService.IsManagedExtrusionLayerPath).ToArray();
        Require(curveLayers.Length == 18 && curveLayers.All(path =>
                path == CurveBranch || path.StartsWith(CurveBranch + "::", StringComparison.Ordinal)),
            "Managed curve scope must be exactly the ABC Curves - PNL subtree: " + string.Join(", ", curveLayers));
        string[] stepLayers = fixture.Layers.Where(PanelCladdingSpawnPlanningService.IsManagedSurfaceLayerPath).ToArray();
        Require(stepLayers.Length == 21 && stepLayers.All(path =>
                path == StepRoot || path.StartsWith(StepRoot + "::", StringComparison.Ordinal)),
            "Managed surface scope must be exactly the Reference::Surfaces - STEP subtree: " + string.Join(", ", stepLayers));
        Require(!fixture.Layers.Any(path => PanelCladdingPidPlanningService.IsPanelLayerPath(path) &&
                (PanelCladdingSpawnPlanningService.IsManagedExtrusionLayerPath(path) ||
                 PanelCladdingSpawnPlanningService.IsManagedSurfaceLayerPath(path))),
            "Source panels must never be classified as managed dependencies.");

        foreach (string path in new[]
                 {
                     "", "Reference", "Reference::MTL_guids", "Reference::Surfaces - STEP Detail::GLS-001",
                     Root + "::Surfaces - GLS::GLS-005.0", "04_STEP Surfaces::Surfaces-Glass::GL01", "Other::" + StepRoot
                 })
        {
            Require(!PanelCladdingSpawnPlanningService.IsManagedSurfaceLayerPath(path), "Exclude STEP scope: " + path);
        }
        foreach (string path in new[]
                 {
                     "", Root, Root + "::Curves - PNL Detail::Curves - WT_01", "02_CW Extrusions::Curves-PNL::WT-04",
                     "Reference::Curves - PNL", PanelBranch + "::Surfaces - WT_04"
                 })
        {
            Require(!PanelCladdingSpawnPlanningService.IsManagedExtrusionLayerPath(path), "Exclude curve scope: " + path);
        }
        Require(PanelCladdingSpawnPlanningService.IsManagedExtrusionLayerPath("QRS-IEF-Wireframe::Curves - PNL::Curves - WT_02"),
            "Managed curve scope must follow any project root.");

        // ABC keeps CID-bearing glass copies in the wireframe tree; they must not look like managed duplicates.
        CladdingLayer[] wireframeCopies = fixture.CladdingLayers
            .Where(row => row.Layer.StartsWith(Root + "::", StringComparison.Ordinal)).ToArray();
        Require(wireframeCopies.Length > 0 && wireframeCopies.All(row =>
                !PanelCladdingSpawnPlanningService.IsManagedSurfaceLayerPath(row.Layer)),
            "Wireframe Surfaces - GLS copies must stay outside the managed STEP scope.");
        Console.WriteLine("[OK] managed curves are <PROJECT>-IEF-Wireframe::Curves - PNL; managed surfaces are Reference::Surfaces - STEP");
    }

    private static void VerifyStepMaterialLayers(Fixture fixture)
    {
        CladdingLayer[] stepRows = fixture.CladdingLayers
            .Where(row => row.Layer.StartsWith(StepRoot + "::", StringComparison.Ordinal)).ToArray();
        Require(stepRows.Length == 15, "Fixture must contain the fifteen live ABC STEP material layers.");
        foreach (CladdingLayer row in stepRows)
        {
            string planned = PanelCladdingSpawnPlanningService.BuildMaterialLayerPath(row.Cladding);
            Require(planned == row.Layer,
                $"{row.Cladding} plans {planned}, but the live document keeps its {row.Count} surfaces on {row.Layer}.");
        }

        (string Code, string Family)[] families =
        {
            ("GLS-001", "Surfaces - GLS"), ("MPL-002", "Surfaces - MPL"), ("STN-003", "Surfaces - STN"),
            ("TER-001", "Surfaces - TER"), ("LVR-001", "Surfaces - LVR"), ("MTL-05.1-A", "Surfaces - MTL"),
            ("gl01", "Surfaces - GL"), (" custom01 ", "Surfaces - CUSTOM"), ("001", "Surfaces - Other"), ("", "Surfaces - Other")
        };
        foreach ((string code, string family) in families)
        {
            Require(PanelCladdingSpawnPlanningService.ResolveMaterialFamilyLayer(code) == family,
                $"Material family layer for '{code}' must be {family}.");
        }
        Console.WriteLine("[OK] cladding surfaces plan Reference::Surfaces - STEP::Surfaces - <prefix>::<material>");
    }

    private static void VerifySurfaceSyncLayerValidation(Fixture fixture)
    {
        MethodInfo resolve = typeof(PanelCladdingSurfaceSyncPlanningService).GetMethod(
            "ResolveLayerMaterial", BindingFlags.NonPublic | BindingFlags.Static) ??
            throw new InvalidOperationException("Surface sync layer validation entry point is missing.");
        OperationResponse<string> Resolve(string layer) =>
            (OperationResponse<string>)resolve.Invoke(null, new object[] { layer, "CID-TEST_0A" })!;

        foreach (CladdingLayer row in fixture.CladdingLayers)
        {
            OperationResponse<string> response = Resolve(row.Layer);
            if (row.Layer.StartsWith(StepRoot + "::", StringComparison.Ordinal))
            {
                Require(response.Success && response.Data == row.Cladding,
                    $"PCSyncSrf must read {row.Cladding} from {row.Layer}: {response.Message}");
            }
            else
            {
                Require(!response.Success && response.Message.StartsWith("PANEL_CLADDING_SURFACE_SYNC_LAYER_INVALID", StringComparison.Ordinal),
                    "A non-STEP cladding layer must not be read as material: " + row.Layer);
            }
        }
        OperationResponse<string> caseInsensitive = Resolve("reference::surfaces - step::surfaces - gls::gls-001");
        Require(caseInsensitive.Success && caseInsensitive.Data == "GLS-001", "STEP layer matching must ignore case.");
        OperationResponse<string> mismatch = Resolve(StepRoot + "::Surfaces - MPL::GLS-001");
        Require(!mismatch.Success && mismatch.Message.StartsWith("PANEL_CLADDING_SURFACE_SYNC_MATERIAL_FAMILY_MISMATCH", StringComparison.Ordinal) &&
                mismatch.Message.Contains("Surfaces - GLS", StringComparison.Ordinal),
            "A material on the wrong prefix layer must report its expected family.");
        foreach (string invalid in new[]
                 {
                     "04_STEP Surfaces::Surfaces-Glass::GL01", "03_Material Surfaces (STEP)::Surfaces-Glass::GL01",
                     StepRoot + "::GLS-001", StepRoot + "::Surfaces - GLS::GLS-001::Extra", StepRoot
                 })
        {
            OperationResponse<string> response = Resolve(invalid);
            Require(!response.Success && response.Message.Contains(StepRoot + "::<family>::<material>", StringComparison.Ordinal),
                "Invalid STEP layer shape must fail with the expected pattern: " + invalid);
        }
        Console.WriteLine("[OK] PCSyncSrf reads materials only from Reference::Surfaces - STEP::<family>::<material>");
    }

    private static void VerifyMaterialCategories()
    {
        (string Code, string? Category)[] categories =
        {
            ("GLS-001", "Glass"), ("gl01", "Glass"), ("MPL-001", "Metal"), ("MTL-05.1-A", "Metal"),
            ("STN-002", "Stone"), ("TER-001", "Terracotta"), ("GFRC-01", "Concrete"), ("WD01", "Wood"),
            ("LVR-001", null), ("custom01", null)
        };
        foreach ((string code, string? category) in categories)
        {
            Require(PanelCladdingSpawnPlanningService.ResolveMaterialCategory(code) == category,
                $"Material category for {code} must be {category ?? "<none>"}.");
        }
        Require(PanelCladdingSpawnPlanningService.ResolveMaterialLayerColor("GLS-001") == new PanelColorRgb(0x59, 0x83, 0x90) &&
                PanelCladdingSpawnPlanningService.ResolveMaterialLayerColor("GLS-002") == new PanelColorRgb(0x46, 0x68, 0x73),
            "Glass codes must keep the calibrated glass palette.");
        Require(PanelCladdingSpawnPlanningService.ResolveMaterialLayerColor("MPL-001") ==
                PanelCladdingSpawnPlanningService.ResolveMaterialLayerColor("ALU-001") &&
                PanelCladdingSpawnPlanningService.ResolveMaterialLayerColor("LVR-001") ==
                PanelCladdingSpawnPlanningService.ResolveMaterialLayerColor("CUSTOM-001"),
            "Default colours must depend on the material category, not the new prefix layer name.");

        MethodInfo createItem = typeof(PanelCladdingKeyService).Assembly
            .GetType("PanelCladdingEditor.Infrastructure.PanelCladding.OpenXmlPanelCladdingWorkbookRepository")?
            .GetMethod("CreateDefaultMaterialCatalogItem", BindingFlags.NonPublic | BindingFlags.Static) ??
            throw new InvalidOperationException("Workbook default material item factory is missing.");
        PanelCladdingMaterialCatalogItem Item(string code) => (PanelCladdingMaterialCatalogItem)createItem.Invoke(null, new object[] { code })!;
        Require(Item("GLS-001").Category == "Glass" && Item("STN-001").Category == "Stone" &&
                Item("MPL-001").Description == "Metal panel" && Item("LVR-001").Category == "Composite",
            "Workbook material defaults must keep their categories.");
        Console.WriteLine("[OK] material categories, default colours and workbook defaults are independent of layer names");
    }

    private static void VerifyAbcSpawnPlan()
    {
        // ABC E2-03-02 on Surfaces - WT_11: MPL-001 / LVR-001 / MPL-001 cladding over a 90 x 224.625 unit.
        var userText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CW_1.01_PID"] = "PID-ABC-E2-03-02",
            ["CW_1.02_CID"] = "CID-ABC-E2-03-02",
            ["CW_1.05_LOT"] = "REL-ABC-PNL-02.01",
            ["CW_1.06_UNIT_TYPE"] = "flat",
            ["CW_1.07_WALL_TYPE"] = "WT-11",
            ["CW_1.08_FRAME_CONFIG"] = "{\"v\":1,\"delete\":\"AQECAAEAAgADAH8=\",\"merge\":\"AQICAAEAAgADAAw=\",\"hide\":\"AQMCAAEAAgADAAA=\"}",
            ["CW_2.03_OFFSET_H0"] = "22.625",
            ["CW_2.03_OFFSET_H1"] = "134.875",
            ["CW_2.04_OFFSET_V0"] = "45",
            ["CW_4.00_CLADDING_0A"] = "MPL-001",
            ["CW_4.00_CLADDING_0B"] = "LVR-001",
            ["CW_4.00_CLADDING_0C"] = "MPL-001",
            ["CW_4.01_CLADDING_1A"] = "0A",
            ["CW_4.01_CLADDING_1B"] = "0B",
            ["CW_4.01_CLADDING_1C"] = "0C",
            ["CW_2.13_CLADDING_LOGIC"] = "{\"0A\":\"\",\"0B\":\"\",\"0C\":\"\",\"1A\":\"0A\",\"1B\":\"0B\",\"1C\":\"0C\"}"
        };
        var keys = new PanelCladdingKeyService();
        PanelCladdingKeySet keySet = Required(keys.Parse(CladdingLogicFixture.WithLogic(userText), 90d, 224.625d, 0.001d), "parse ABC E2-03-02");
        PanelCladdingSpawnPlan plan = Required(
            new PanelCladdingSpawnPlanningService(keys).CreatePlan(
                userText, keySet, 90d, 224.625d, PanelBranch + "::Surfaces - WT_11"),
            "plan ABC E2-03-02");
        Require(plan.Regions.Count > 0 && plan.Regions.All(region =>
                region.LayerPath == StepRoot + "::Surfaces - " + region.CladdingCode[..3] + "::" + region.CladdingCode),
            "Spawned cladding must route to the STEP prefix layers: " + string.Join(", ", plan.Regions.Select(r => r.LayerPath)));
        Require(plan.Regions.Select(region => region.LayerPath).ToHashSet().SetEquals(new[]
                { StepRoot + "::Surfaces - MPL::MPL-001", StepRoot + "::Surfaces - LVR::LVR-001" }),
            "ABC E2-03-02 must use exactly the MPL-001 and LVR-001 STEP layers.");
        Require(plan.Curves.Count > 4 && plan.Curves.All(curve => curve.LayerPath == CurveBranch + "::Curves - WT_11"),
            "Spawned curves must route to Curves - WT_11: " + string.Join(", ", plan.Curves.Select(c => c.LayerPath).Distinct()));

        OperationResponse<PanelCladdingSpawnPlan> legacy = new PanelCladdingSpawnPlanningService(keys).CreatePlan(
            userText, keySet, 90d, 224.625d, "01_CW Panels::Surfaces-PNL::WT-11");
        Require(!legacy.Success && legacy.Message.StartsWith("PANEL_CLADDING_PANEL_LAYER_NOT_SUPPORTED", StringComparison.Ordinal),
            "A panel on the retired layer tree must fail before any curve is planned.");
        Console.WriteLine("[OK] a live ABC panel plans STEP surfaces and Curves - WT_11 extrusion curves");
    }

    private static void VerifyNoLegacyLayerNamesInSource()
    {
        string sourceRoot = Path.Combine(FindRepositoryRoot(), "src", "PanelCladdingEditor");
        string[] retired = { "01_CW Panels", "02_CW Extrusions", "04_STEP Surfaces", "03_Material Surfaces", "Surfaces-PNL", "Curves-PNL" };
        string[] hits = Directory.EnumerateFiles(sourceRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or "bin-testhost" or "obj-testhost"))
            .SelectMany(path => retired.Where(name => File.ReadAllText(path).Contains(name, StringComparison.OrdinalIgnoreCase))
                .Select(name => $"{Path.GetFileName(path)}: {name}"))
            .ToArray();
        Require(hits.Length == 0, "Retired layer names remain in plug-in source: " + string.Join("; ", hits));
        Console.WriteLine("[OK] plug-in source no longer references the retired 01/02/04 layer tree");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new InvalidOperationException("Repository root containing AGENTS.md was not found.");
    }

    private static T Required<T>(OperationResponse<T> response, string operation)
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

    private sealed record Fixture(
        IReadOnlyList<string> Layers,
        IReadOnlyList<LayerPair> PanelCurveLayerPairs,
        IReadOnlyList<CladdingLayer> CladdingLayers);

    private sealed record LayerPair(string PanelLayer, string CurveLayer, int PidCount);

    private sealed record CladdingLayer(string Cladding, string Layer, int Count);
}
