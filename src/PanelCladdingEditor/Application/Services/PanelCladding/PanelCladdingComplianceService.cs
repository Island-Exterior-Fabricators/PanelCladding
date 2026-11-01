using System.Globalization;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

// PCreport's rules. Each rule reuses the predicate or service the commands use, so the report and
// the commands agree on what is compliant. Objects the plug-in does not manage (wireframe copies
// such as Surfaces - GLS, even with a PID) are not checked. Pure and read-only.
public sealed class PanelCladdingComplianceService
{
    private const string CladdingUserTextKey = "Cladding";
    private static readonly string[] RetiredRootLayers =
        ["01_CW Panels", "02_CW Extrusions", "03_Material Surfaces (STEP)", "04_STEP Surfaces"];
    private static readonly string[] OtherLegacyKeys = ["Plane", "Config", "AUTO_panel"];
    private static readonly string[] UnitTypes =
    [
        PanelCladdingCidService.CurvedUnitType, PanelCladdingCidService.FlatUnitType,
        PanelCladdingCidService.TwoFaceUnitType, PanelCladdingCidService.ThreeFaceUnitType
    ];

    public static readonly PanelCladdingReportRule ProjectRootMissing = Layer("L01", "Project root layer missing",
        PanelCladdingReportSeverity.Error, "Add the top-level <PROJECT>-IEF-Wireframe layer; the project code comes from the file name.");
    public static readonly PanelCladdingReportRule RetiredLayerTree = Layer("L02", "Retired layer tree",
        PanelCladdingReportSeverity.Error, "Move its objects to the current layer standard; the plug-in ignores this tree.");
    public static readonly PanelCladdingReportRule PanelWithoutTypeLayer = Layer("L03", "Panel without a type layer",
        PanelCladdingReportSeverity.Error, "Move the panel to Surfaces - PNL::Surfaces - <type>; PCbake cannot place its curves.");
    public static readonly PanelCladdingReportRule NearStandardName = Layer("L04", "Layer name close to the standard",
        PanelCladdingReportSeverity.Warning, "Rename the layer to the standard name; objects on it are not tracked.");
    public static readonly PanelCladdingReportRule StepFamilyMismatch = Layer("L05", "STEP family does not match the material",
        PanelCladdingReportSeverity.Warning, "Use Reference::Surfaces - STEP::Surfaces - <prefix>::<material>.");
    public static readonly PanelCladdingReportRule CurveLayerWithoutPanelLayer = Layer("L06", "Curve type layer without a panel type layer",
        PanelCladdingReportSeverity.Info, "Add the matching Surfaces - <type> layer or move the curves.");
    public static readonly PanelCladdingReportRule NotTrackedLayer = Layer("L07", "Not tracked layer",
        PanelCladdingReportSeverity.Info, "Layers named Not in Scope or By Others are ignored by the plug-in.");

    public static readonly PanelCladdingReportRule KeyCaseDuplicate = Attribute("A01", "Key repeated in another case",
        PanelCladdingReportSeverity.Error, "Keep one spelling of the key and delete the other.");
    public static readonly PanelCladdingReportRule DuplicateCid = Attribute("A02", "Duplicate CID",
        PanelCladdingReportSeverity.Error, "Run PCpid on these panels; PCbake skips panels with duplicate CIDs.");
    public static readonly PanelCladdingReportRule UnreadableData = Attribute("A03", "Cladding or frame data unreadable",
        PanelCladdingReportSeverity.Error, "Open the panel in PCeditor and save, or rerun PCcreate.");
    public static readonly PanelCladdingReportRule PidFormat = Attribute("A04", "PID does not follow PID-<project>-…",
        PanelCladdingReportSeverity.Warning, "Run PCpid.");
    public static readonly PanelCladdingReportRule CidMismatch = Attribute("A05", "CID does not match PID and role",
        PanelCladdingReportSeverity.Warning, "Run PCpid, or any command that writes the panel.");
    public static readonly PanelCladdingReportRule NameMismatch = Attribute("A06", "Name does not match CID",
        PanelCladdingReportSeverity.Warning, "Run PCbake or save in PCeditor; names follow the CID.");
    public static readonly PanelCladdingReportRule UnitTypeInvalid = Attribute("A07", "Unit type not C, F, L or Z",
        PanelCladdingReportSeverity.Warning, "Set CW_1.06_UNIT_TYPE to C, F, L or Z.");
    public static readonly PanelCladdingReportRule RoleMismatch = Attribute("A08", "Role does not match unit type",
        PanelCladdingReportSeverity.Warning, "Set CW_5.00_DEFINITION_A: parent for C and F; parent, child or child <n> for L and Z.");
    public static readonly PanelCladdingReportRule FrameTypePayload = Attribute("A09", "Frame type holds an old payload",
        PanelCladdingReportSeverity.Warning, "Replace CW_1.09_FRAME_TYPE with the hand-entered frame type.");
    public static readonly PanelCladdingReportRule MissingSetupKeys = Attribute("A10", "Setup keys missing",
        PanelCladdingReportSeverity.Info, "Run PCpid to add blank placeholders.");
    public static readonly PanelCladdingReportRule LegacyKeys = Attribute("A11", "Legacy keys",
        PanelCladdingReportSeverity.Info, "Migrated keys are renamed on the next panel write; the others are ignored.");
    public static readonly PanelCladdingReportRule RoleCase = Attribute("A12", "Role not lowercase",
        PanelCladdingReportSeverity.Info, "Any command that writes the panel lowercases it.");
    public static readonly PanelCladdingReportRule PlaneMissing = Attribute("A13", "UD_PLANE missing or unreadable",
        PanelCladdingReportSeverity.Info, "Reopen the model; auto attributes refresh it.");

    public static readonly PanelCladdingReportRule OrphanDependency = Dependency("D01", "No owner panel",
        PanelCladdingReportSeverity.Warning, "Delete it, or fix its PID and CID so that a tracked panel owns it.");
    public static readonly PanelCladdingReportRule NotTrackedOwner = Dependency("D02", "Owner panel not tracked",
        PanelCladdingReportSeverity.Info, "Its panel is on a Not in Scope or By Others layer.");
    public static readonly PanelCladdingReportRule DuplicateDependencyCid = Dependency("D03", "Duplicate dependency CID",
        PanelCladdingReportSeverity.Warning, "PCbake removes duplicate surfaces; delete extra curves by hand.");
    public static readonly PanelCladdingReportRule CladdingLayerMismatch = Dependency("D04", "Cladding value differs from its layer",
        PanelCladdingReportSeverity.Warning, "Run PCsyncSrf to read the layer back, or PCbake to rebuild the surface.");
    public static readonly PanelCladdingReportRule CurveLayerMismatch = Dependency("D05", "Curve not on its panel's curve layer",
        PanelCladdingReportSeverity.Warning, "Move the curve to its panel's Curves - <type> layer.");
    public static readonly PanelCladdingReportRule PanelNotBaked = Dependency("D06", "Panel with materials but no surfaces",
        PanelCladdingReportSeverity.Info, "Run PCbake.");

    public static IReadOnlyList<PanelCladdingReportRule> Rules { get; } = Array.AsReadOnly(new[]
    {
        ProjectRootMissing, RetiredLayerTree, PanelWithoutTypeLayer, NearStandardName, StepFamilyMismatch,
        CurveLayerWithoutPanelLayer, NotTrackedLayer,
        KeyCaseDuplicate, DuplicateCid, UnreadableData, PidFormat, CidMismatch, NameMismatch, UnitTypeInvalid,
        RoleMismatch, FrameTypePayload, MissingSetupKeys, LegacyKeys, RoleCase, PlaneMissing,
        OrphanDependency, NotTrackedOwner, DuplicateDependencyCid, CladdingLayerMismatch, CurveLayerMismatch,
        PanelNotBaked
    });

    private readonly PanelCladdingKeyService _keys = new();

    public PanelCladdingReport Check(PanelCladdingReportSnapshot snapshot, DateTime createdAt)
    {
        string project = PanelCladdingProjectService.CodeFromDocumentPath(snapshot.DocumentPath);
        string[] layers = snapshot.LayerPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var issues = new List<PanelCladdingReportIssue>();

        Panel[] panels = snapshot.Objects
            .Where(item => item.Kind == PanelCladdingReportObjectKind.Brep &&
                PanelCladdingExtrusionPlanningService.IsPanelLayerPath(item.LayerPath, project))
            .Select(item => new Panel(item, Pid(item), ResolvedCid(item)))
            .ToArray();
        Panel[] notTrackedPanels = snapshot.Objects
            .Where(item => item.Kind == PanelCladdingReportObjectKind.Brep &&
                IsBranch(item.LayerPath, PanelCladdingExtrusionPlanningService.PanelSurfaceLayerName, project) &&
                PanelCladdingExtrusionPlanningService.IsExcludedLayerPath(item.LayerPath))
            .Select(item => new Panel(item, Pid(item), ResolvedCid(item)))
            .ToArray();
        PanelCladdingReportObject[] surfaces = snapshot.Objects
            .Where(item => item.Kind == PanelCladdingReportObjectKind.Brep &&
                PanelCladdingSpawnPlanningService.IsManagedSurfaceLayerPath(item.LayerPath))
            .ToArray();
        PanelCladdingReportObject[] curves = snapshot.Objects
            .Where(item => item.Kind == PanelCladdingReportObjectKind.Curve &&
                PanelCladdingSpawnPlanningService.IsManagedExtrusionLayerPath(item.LayerPath, project))
            .ToArray();

        CheckLayers(layers, snapshot.Objects, panels, project, issues);
        foreach (Panel panel in panels)
        {
            CheckPanel(panel, project, snapshot.Tolerance, issues);
        }
        foreach (IGrouping<string, Panel> group in panels
                     .Where(panel => panel.Cid.Length > 0)
                     .GroupBy(panel => panel.Cid, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            foreach (Panel panel in group)
            {
                issues.Add(Issue(DuplicateCid, panel.Object,
                    $"{group.Key} is shared by {group.Count()} panels"));
            }
        }
        CheckDependencies(panels, notTrackedPanels, surfaces, curves, project, issues);

        var order = Rules.Select((rule, index) => (rule.Id, index)).ToDictionary(item => item.Id, item => item.index);
        return new PanelCladdingReport
        {
            DocumentPath = snapshot.DocumentPath,
            ProjectCode = project,
            CreatedAt = createdAt,
            PanelCount = panels.Length,
            SurfaceCount = surfaces.Length,
            CurveCount = curves.Length,
            Issues = issues
                .OrderBy(issue => order[issue.Rule.Id])
                .ThenBy(issue => issue.LayerPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(issue => issue.ObjectName, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    private static void CheckLayers(
        IReadOnlyList<string> layers,
        IReadOnlyList<PanelCladdingReportObject> objects,
        IReadOnlyList<Panel> panels,
        string project,
        List<PanelCladdingReportIssue> issues)
    {
        string root = project.Length > 0
            ? PanelCladdingProjectService.RootLayerName(project)
            : "<PROJECT>" + PanelCladdingProjectService.RootLayerSuffix;
        if (!layers.Any(path => !path.Contains("::", StringComparison.Ordinal) &&
                PanelCladdingExtrusionPlanningService.IsProjectRootLayer(path, project)))
        {
            issues.Add(LayerIssue(ProjectRootMissing, string.Empty, $"Expected the top-level layer {root}"));
        }

        foreach (string retired in layers.Where(path => RetiredRootLayers.Any(name =>
                     string.Equals(path, name, StringComparison.OrdinalIgnoreCase))))
        {
            issues.Add(LayerIssue(RetiredLayerTree, retired, $"{CountAtOrBelow(objects, retired)} object(s)"));
        }

        foreach (Panel panel in panels.Where(panel => Segments(panel.Object.LayerPath).Length == 2))
        {
            issues.Add(Issue(PanelWithoutTypeLayer, panel.Object, "On the Surfaces - PNL branch itself"));
        }

        string[] standardNames =
        [
            root, PanelCladdingExtrusionPlanningService.PanelSurfaceLayerName,
            PanelCladdingExtrusionPlanningService.CurveLayerName, "Surfaces - STEP"
        ];
        foreach (string path in layers.Where(path => !RetiredRootLayers.Any(retired => IsAtOrBelow(path, retired))))
        {
            string leaf = Segments(path)[^1];
            string? standard = standardNames.FirstOrDefault(name =>
                LayerWord(name) == LayerWord(leaf) && !string.Equals(name, leaf, StringComparison.OrdinalIgnoreCase));
            if (standard is not null && !standard.StartsWith('<'))
            {
                issues.Add(LayerIssue(NearStandardName, path, $"'{leaf}' should be '{standard}'"));
            }
        }

        foreach (string path in layers)
        {
            string[] segments = Segments(path);
            if (segments.Length == 4 && PanelCladdingSpawnPlanningService.IsManagedSurfaceLayerPath(path))
            {
                string expected = PanelCladdingSpawnPlanningService.BuildMaterialLayerPath(segments[3]);
                if (!string.Equals(expected, path, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(LayerIssue(StepFamilyMismatch, path, $"Expected {expected}"));
                }
            }
        }

        var layerSet = layers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string path in layers.Where(path => Segments(path).Length == 3 &&
                     PanelCladdingExtrusionPlanningService.IsCurveLayerPath(path, project)))
        {
            string[] segments = Segments(path);
            string type = segments[2].StartsWith(PanelCladdingExtrusionPlanningService.CurveTypeLayerPrefix, StringComparison.OrdinalIgnoreCase)
                ? PanelCladdingExtrusionPlanningService.PanelTypeLayerPrefix + segments[2][PanelCladdingExtrusionPlanningService.CurveTypeLayerPrefix.Length..]
                : segments[2];
            string panelLayer = $"{segments[0]}::{PanelCladdingExtrusionPlanningService.PanelSurfaceLayerName}::{type}";
            bool holdsPluginCurves = objects.Any(item => item.Kind == PanelCladdingReportObjectKind.Curve &&
                IsAtOrBelow(item.LayerPath, path) &&
                Get(item.UserText, PanelCladdingSpawnPlanningService.CidUserTextKey).Trim().Length > 0);
            if (holdsPluginCurves && !layerSet.Contains(panelLayer))
            {
                issues.Add(LayerIssue(CurveLayerWithoutPanelLayer, path, $"No {panelLayer}"));
            }
        }

        foreach (string path in layers.Where(path => PanelCladdingExtrusionPlanningService.IsExcludedLayerPath(path)))
        {
            string parent = path.Contains("::", StringComparison.Ordinal) ? path[..path.LastIndexOf("::", StringComparison.Ordinal)] : string.Empty;
            if (parent.Length == 0 || !PanelCladdingExtrusionPlanningService.IsExcludedLayerPath(parent))
            {
                issues.Add(LayerIssue(NotTrackedLayer, path, $"{CountAtOrBelow(objects, path)} object(s) not tracked"));
            }
        }
    }

    private void CheckPanel(Panel panel, string project, double tolerance, List<PanelCladdingReportIssue> issues)
    {
        IReadOnlyDictionary<string, string> text = panel.Object.UserText;
        foreach (IGrouping<string, string> group in text.Keys
                     .GroupBy(key => key, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            issues.Add(Issue(KeyCaseDuplicate, panel.Object, string.Join(", ", group)));
        }

        if (TryNumber(Get(text, PanelCladdingKeyService.UnitWidthKey), out double width) &&
            TryNumber(Get(text, PanelCladdingKeyService.UnitHeightKey), out double height))
        {
            OperationResponse<PanelCladdingKeySet> parsed = _keys.Parse(text, width, height, tolerance);
            if (!parsed.Success)
            {
                issues.Add(Issue(UnreadableData, panel.Object, parsed.Message));
            }
        }

        string storedCid = Get(text, PanelCladdingSpawnPlanningService.CidUserTextKey).Trim();
        if (panel.Pid.Length > 0)
        {
            string prefix = PanelCladdingCidService.PanelIdPrefix +
                (project.Length > 0 ? project + PanelCladdingCidService.IdSeparator : string.Empty);
            if (!panel.Pid.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Issue(PidFormat, panel.Object, $"{panel.Pid}; expected {prefix}…"));
            }
            if (storedCid.Length > 0 && !string.Equals(storedCid, panel.Cid, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Issue(CidMismatch, panel.Object, $"{storedCid}; expected {panel.Cid}"));
            }
        }
        if (panel.Cid.Length > 0)
        {
            string expectedName = PanelCladdingCidService.ShortName(panel.Cid);
            if (!string.Equals(panel.Object.Name.Trim(), expectedName, StringComparison.Ordinal))
            {
                issues.Add(Issue(NameMismatch, panel.Object,
                    $"'{panel.Object.Name}'; expected '{expectedName}'"));
            }
        }

        string unitType = Get(text, PanelCladdingCidService.UnitTypeUserTextKey).Trim();
        string role = Get(text, PanelCladdingCidService.RoleDefinitionKey).Trim();
        if (unitType.Length > 0 && !UnitTypes.Contains(unitType.ToUpperInvariant(), StringComparer.Ordinal))
        {
            issues.Add(Issue(UnitTypeInvalid, panel.Object, $"CW_1.06_UNIT_TYPE = {unitType}"));
        }
        else if (PanelCladdingCidService.IsMultiFaceUnitType(unitType) && !PanelCladdingCidService.IsRole(role))
        {
            issues.Add(Issue(RoleMismatch, panel.Object,
                $"{unitType.ToUpperInvariant()} panel with Definition A '{role}'"));
        }
        else if (unitType.Length > 0 && !PanelCladdingCidService.IsMultiFaceUnitType(unitType) && role.Length > 0 &&
                 !string.Equals(role, PanelCladdingCidService.ParentRole, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Issue(RoleMismatch, panel.Object,
                $"{unitType.ToUpperInvariant()} panel with Definition A '{role}'; expected parent"));
        }
        if (PanelCladdingCidService.IsRole(role) && !string.Equals(role, role.ToLowerInvariant(), StringComparison.Ordinal))
        {
            issues.Add(Issue(RoleCase, panel.Object, $"Definition A '{role}'"));
        }

        if (Get(text, PanelCladdingKeyService.FrameTypeKey).TrimStart().StartsWith('{'))
        {
            issues.Add(Issue(FrameTypePayload, panel.Object, "CW_1.09_FRAME_TYPE starts with '{'"));
        }

        string[] missing = PanelCladdingPidSetupService.RequiredKeys
            .Where(key => !text.Keys.Any(existing => string.Equals(existing, key, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (missing.Length > 0)
        {
            issues.Add(Issue(MissingSetupKeys, panel.Object, string.Join(", ", missing)));
        }

        string[] legacy = text.Keys.Where(IsLegacyKey).OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToArray();
        if (legacy.Length > 0)
        {
            issues.Add(Issue(LegacyKeys, panel.Object, string.Join(", ", legacy)));
        }

        string plane = Get(text, PanelCladdingKeyService.PlaneKey).Trim();
        if (!PanelCladdingPlaneFormat.TryParse(plane, out _))
        {
            issues.Add(Issue(PlaneMissing, panel.Object, plane.Length == 0 ? "Missing" : $"'{plane}'"));
        }
    }

    private void CheckDependencies(
        IReadOnlyList<Panel> panels,
        IReadOnlyList<Panel> notTrackedPanels,
        IReadOnlyList<PanelCladdingReportObject> surfaces,
        IReadOnlyList<PanelCladdingReportObject> curves,
        string project,
        List<PanelCladdingReportIssue> issues)
    {
        ILookup<string, Panel> trackedByPid = panels.ToLookup(panel => panel.Pid, StringComparer.OrdinalIgnoreCase);
        ILookup<string, Panel> notTrackedByPid = notTrackedPanels.ToLookup(panel => panel.Pid, StringComparer.OrdinalIgnoreCase);
        var bakedPanels = new HashSet<Guid>();
        foreach ((PanelCladdingReportObject dependency, bool isCurve) in surfaces.Select(item => (item, false))
                     .Concat(curves.Select(item => (item, true))))
        {
            string pid = Pid(dependency);
            string cid = Get(dependency.UserText, PanelCladdingSpawnPlanningService.CidUserTextKey).Trim();
            if (pid.Length == 0 || cid.Length == 0)
            {
                issues.Add(Issue(OrphanDependency, dependency, pid.Length == 0 ? "No PID" : $"{pid}; no CID"));
                continue;
            }
            Panel[] owners = Owners(trackedByPid, pid, cid);
            if (owners.Length == 0)
            {
                issues.Add(Owners(notTrackedByPid, pid, cid).Length > 0
                    ? Issue(NotTrackedOwner, dependency, cid)
                    : Issue(OrphanDependency, dependency, cid));
                continue;
            }
            if (owners.Length > 1)
            {
                continue;
            }
            Panel owner = owners[0];
            if (!isCurve)
            {
                bakedPanels.Add(owner.Object.Id);
                string cladding = Get(dependency.UserText, CladdingUserTextKey).Trim();
                string leaf = Segments(dependency.LayerPath)[^1];
                if (cladding.Length > 0 && !string.Equals(cladding, leaf, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(Issue(CladdingLayerMismatch, dependency, $"Cladding {cladding} on layer {leaf}"));
                }
            }
            else
            {
                OperationResponse<string> expected =
                    PanelCladdingExtrusionPlanningService.ResolveCurveLayerPath(owner.Object.LayerPath, project);
                if (expected.Success && expected.Data is string layer &&
                    !string.Equals(layer, dependency.LayerPath, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(Issue(CurveLayerMismatch, dependency, $"Expected {layer}"));
                }
            }
        }

        foreach ((IReadOnlyList<PanelCladdingReportObject> items, string kind) in new[] { (surfaces, "surfaces"), (curves, "curves") })
        {
            foreach (IGrouping<string, PanelCladdingReportObject> group in items
                         .Select(item => (item, cid: Get(item.UserText, PanelCladdingSpawnPlanningService.CidUserTextKey).Trim()))
                         .Where(pair => pair.cid.Length > 0)
                         .GroupBy(pair => pair.cid, pair => pair.item, StringComparer.OrdinalIgnoreCase)
                         .Where(group => group.Count() > 1))
            {
                foreach (PanelCladdingReportObject item in group)
                {
                    issues.Add(Issue(DuplicateDependencyCid, item, $"{group.Count()} {kind} share {group.Key}"));
                }
            }
        }

        foreach (Panel panel in panels.Where(panel => !bakedPanels.Contains(panel.Object.Id) && HasMaterials(panel)))
        {
            issues.Add(Issue(PanelNotBaked, panel.Object, panel.Cid));
        }
    }

    private bool HasMaterials(Panel panel)
    {
        if (!PanelCladdingKeyService.IsCladdingConfigured(panel.Object.UserText))
        {
            return false;
        }
        OperationResponse<IReadOnlyDictionary<string, string>> values = _keys.ReadCladdingCellValues(panel.Object.UserText);
        return values.Success && values.Data is not null && values.Data.Values.Any(value =>
            !string.IsNullOrWhiteSpace(value) && !PanelCladdingKeyService.IsCellLabelToken(value));
    }

    private static Panel[] Owners(ILookup<string, Panel> panelsByPid, string pid, string cid) => panelsByPid[pid]
        .Where(panel => PanelCladdingCidService.IncludesDependency(panel.Pid, panel.Cid, cid))
        .ToArray();

    private static bool IsLegacyKey(string key) =>
        PanelCladdingKeyService.IsRetiredFrameKey(key) ||
        PanelCladdingKeyService.IsRetiredCladdingTypeKey(key) ||
        string.Equals(key, PanelCladdingKeyService.LegacyCladdingLogicKey, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, PanelCladdingKeyService.LegacyUserDefinedCladdingLogicKey, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, PanelCladdingKeyService.LegacyUserDefinedFrameConfigKey, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, PanelCladdingKeyService.LegacySignatureKey, StringComparison.OrdinalIgnoreCase) ||
        OtherLegacyKeys.Any(name => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));

    private static string Pid(PanelCladdingReportObject item) =>
        Get(item.UserText, PanelCladdingSpawnPlanningService.PanelIdUserTextKey).Trim();

    // The CID the commands use: derived from PID and role when the unit type allows it.
    private static string ResolvedCid(PanelCladdingReportObject item)
    {
        string pid = Pid(item);
        return pid.Length > 0
            ? PanelCladdingCidService.ResolvePanelCid(pid, item.UserText)
            : Get(item.UserText, PanelCladdingSpawnPlanningService.CidUserTextKey).Trim();
    }

    private static bool IsBranch(string layerPath, string branchName, string project)
    {
        string[] segments = Segments(layerPath);
        return segments.Length >= 2 &&
            PanelCladdingExtrusionPlanningService.IsProjectRootLayer(segments[0], project) &&
            string.Equals(segments[1], branchName, StringComparison.OrdinalIgnoreCase);
    }

    private static int CountAtOrBelow(IReadOnlyList<PanelCladdingReportObject> objects, string layerPath) =>
        objects.Count(item => IsAtOrBelow(item.LayerPath, layerPath));

    private static bool IsAtOrBelow(string layerPath, string root) =>
        string.Equals(layerPath, root, StringComparison.OrdinalIgnoreCase) ||
        layerPath.StartsWith(root + "::", StringComparison.OrdinalIgnoreCase);

    private static string[] Segments(string layerPath) =>
        (layerPath ?? string.Empty).Trim().Split("::", StringSplitOptions.None);

    private static string LayerWord(string value) =>
        new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static bool TryNumber(string value, out double number) =>
        double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) &&
        double.IsFinite(number) && number > 0d;

    private static string Get(IReadOnlyDictionary<string, string> userText, string key) =>
        userText.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;

    private static PanelCladdingReportIssue Issue(PanelCladdingReportRule rule, PanelCladdingReportObject item, string detail) =>
        new(rule, item.Id, item.Name, item.LayerPath, detail);

    private static PanelCladdingReportIssue LayerIssue(PanelCladdingReportRule rule, string layerPath, string detail) =>
        new(rule, Guid.Empty, string.Empty, layerPath, detail);

    private static PanelCladdingReportRule Layer(string id, string title, PanelCladdingReportSeverity severity, string fix) =>
        new(id, title, severity, PanelCladdingReportCategory.Layers, fix);

    private static PanelCladdingReportRule Attribute(string id, string title, PanelCladdingReportSeverity severity, string fix) =>
        new(id, title, severity, PanelCladdingReportCategory.Attributes, fix);

    private static PanelCladdingReportRule Dependency(string id, string title, PanelCladdingReportSeverity severity, string fix) =>
        new(id, title, severity, PanelCladdingReportCategory.Dependencies, fix);

    private sealed record Panel(PanelCladdingReportObject Object, string Pid, string Cid);
}
