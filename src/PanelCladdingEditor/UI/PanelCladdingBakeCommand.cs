extern alias rhinocommon;

using System.Runtime.InteropServices;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using GetObject = rhinocommon::Rhino.Input.Custom.GetObject;
using GetResult = rhinocommon::Rhino.Input.GetResult;
using ObjectType = rhinocommon::Rhino.DocObjects.ObjectType;
using Result = rhinocommon::Rhino.Commands.Result;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoCommand = rhinocommon::Rhino.Commands.Command;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RunMode = rhinocommon::Rhino.Commands.RunMode;

namespace PanelCladdingEditor.UI;

// Bakes the selected panels' saved attributes into managed cladding surfaces and extrusion
// curves (PCsyncSrf/PCsyncCrv go the other way). Formerly PCupdate; same command GUID.
[Guid("F2C41D75-2F88-4CB4-9D1C-CF6BD1D6208A")]
public sealed class PanelCladdingBakeCommand : RhinoCommand
{
    // Option list order matches PanelCladdingBakeScope; index 0 (Surfaces) is the default of every run.
    private static readonly string[] ScopeNames = Enum.GetNames<PanelCladdingBakeScope>();

    public override string EnglishName => "PCbake";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (doc is null || string.IsNullOrWhiteSpace(doc.Path))
        {
            RhinoApp.WriteLine("PCbake requires a saved active document.");
            return Result.Failure;
        }

        TargetSelection selection = ResolveTarget(doc);
        if (!selection.Success)
        {
            RhinoApp.WriteLine(selection.Message);
            return selection.Cancelled ? Result.Cancel : Result.Failure;
        }

        bool bakesSurfaces = PanelCladdingDependencyReconciliationService.Includes(
            selection.Scope, PanelCladdingDependencyKind.Surface);
        OperationResponse<IReadOnlyDictionary<string, PanelColorRgb>> materialColors = bakesSurfaces
            ? ResolveMaterialColors(doc)
            : OperationResponse<IReadOnlyDictionary<string, PanelColorRgb>>.Ok(
                new Dictionary<string, PanelColorRgb>(StringComparer.OrdinalIgnoreCase));
        if (!materialColors.Success || materialColors.Data is null)
        {
            RhinoApp.WriteLine(materialColors.Message);
            return Result.Failure;
        }

        var keys = new PanelCladdingKeyService();
        var projection = new PanelAxonometricProjectionService();
        ILivePanelCladdingRepository layouts = new LivePanelCladdingRepository(keys, projection);
        var spawnPlanning = new PanelCladdingSpawnPlanningService(keys, materialColors.Data);
        ILivePanelCladdingUpdateService update = new LivePanelCladdingUpdateService(
            layouts,
            keys,
            spawnPlanning,
            new PanelCladdingDependencyReconciliationService());
        OperationResponse<PanelCladdingUpdateResult> response = update.Update(
            doc.Path,
            selection.ObjectIds,
            selection.Scope);
        if (!response.Success || response.Data is null)
        {
            RhinoApp.WriteLine(response.Message);
            return Result.Failure;
        }

        PanelCladdingUpdateResult result = response.Data;
        RhinoApp.WriteLine($"Standardized point order on {result.ReorderedPanelIds.Count} selected surface(s).");
        foreach (string warning in result.PointOrderWarnings) RhinoApp.WriteLine("Point order skipped: " + warning);
        RhinoApp.WriteLine(
            $"PCbake ({selection.Scope}) processed {result.SourcePanelIds.Count} panel(s): " +
            $"created {result.CreatedSurfaceIds.Count} surface(s) and {result.CreatedCurveIds.Count} curve(s), " +
            $"updated {result.UpdatedSurfaceIds.Count} surface(s) and {result.UpdatedCurveIds.Count} curve(s), " +
            $"deleted {result.DeletedObjectIds.Count} obsolete or duplicate object(s).");
        if (result.PreservedCurvePanelIds.Count > 0)
        {
            RhinoApp.WriteLine(
                $"Kept the existing curves of {result.PreservedCurvePanelIds.Count} panel(s); " +
                "delete a panel's curves to bake new ones.");
        }
        if (result.DuplicateCidGroups.Count > 0)
        {
            int skippedCount = result.DuplicateCidGroups.Sum(group => group.PanelObjectIds.Count);
            RhinoApp.WriteLine($"Skipped {skippedCount} panel(s) with duplicate CIDs; these panels remain selected for inspection.");
            foreach (PanelCladdingDuplicateCidGroup group in result.DuplicateCidGroups)
            {
                RhinoApp.WriteLine($"Duplicate CID {group.Cid}: {group.PanelObjectIds.Count} panels.");
            }
        }
        if (result.PreservedAmbiguousDependencyIds.Count > 0)
        {
            RhinoApp.WriteLine($"Left {result.PreservedAmbiguousDependencyIds.Count} legacy dependencies unchanged because their CID does not identify a single selected panel.");
        }
        return Result.Success;
    }

    internal static OperationResponse<IReadOnlyDictionary<string, PanelColorRgb>> ResolveMaterialColors(
        RhinoDoc document)
    {
        string workbookPath = document.Strings.GetValue(
            PanelCladdingKeyService.WorkbookPathDocumentKey)?.Trim() ?? string.Empty;
        if (workbookPath.Length == 0)
        {
            return OperationResponse<IReadOnlyDictionary<string, PanelColorRgb>>.Ok(
                new Dictionary<string, PanelColorRgb>(StringComparer.OrdinalIgnoreCase));
        }

        var repository = new OpenXmlPanelCladdingWorkbookRepository();
        OperationResponse<PanelCladdingMaterialCatalog> catalog =
            repository.ReadMaterialCatalog(workbookPath);
        if (!catalog.Success || catalog.Data is null)
        {
            return OperationResponse<IReadOnlyDictionary<string, PanelColorRgb>>.Fail(
                $"PANEL_CLADDING_MATERIAL_COLORS_UNAVAILABLE: {catalog.Message}");
        }

        OperationResponse<IReadOnlyDictionary<string, PanelColorRgb>> colors =
            new PanelCladdingMaterialColorService().CreateColorMap(catalog.Data);
        return colors.Success
            ? colors
            : OperationResponse<IReadOnlyDictionary<string, PanelColorRgb>>.Fail(
                $"PANEL_CLADDING_MATERIAL_COLORS_INVALID: {colors.Message}");
    }

    // Preselected panels stay selected and the prompt still offers Scope; Enter accepts.
    private static TargetSelection ResolveTarget(RhinoDoc document)
    {
        int scopeIndex = 0;
        using var getter = new GetObject();
        getter.SetCommandPrompt("Select panel Breps to bake");
        getter.GeometryFilter = ObjectType.Brep;
        getter.GroupSelect = true;
        getter.SubObjectSelect = false;
        getter.EnablePreSelect(enable: true, ignoreUnacceptablePreselectedObjects: true);
        getter.EnableClearObjectsOnEntry(false);
        getter.EnableUnselectObjectsOnExit(false);
        getter.DeselectAllBeforePostSelect = false;
        bool hadPreselectedObjects = false;
        while (true)
        {
            getter.ClearCommandOptions();
            int scopeOption = getter.AddOptionList("Scope", ScopeNames, scopeIndex);
            GetResult selection = getter.GetMultiple(minimumNumber: 1, maximumNumber: 0);
            if (selection == GetResult.Option)
            {
                if (getter.OptionIndex() == scopeOption)
                {
                    scopeIndex = getter.Option().CurrentListOptionIndex;
                }
                getter.EnablePreSelect(enable: false, ignoreUnacceptablePreselectedObjects: true);
                continue;
            }
            if (selection != GetResult.Object)
            {
                return new TargetSelection(
                    false,
                    selection == GetResult.Cancel,
                    Array.Empty<Guid>(),
                    PanelCladdingBakeScope.Surfaces,
                    "Panel bake selection cancelled.");
            }
            if (getter.ObjectsWerePreselected && !hadPreselectedObjects)
            {
                // Offer the option once before running on a preselection.
                hadPreselectedObjects = true;
                getter.EnablePreSelect(enable: false, ignoreUnacceptablePreselectedObjects: true);
                continue;
            }
            break;
        }

        Guid[] objectIds = Enumerable.Range(0, getter.ObjectCount)
            .Select(index => getter.Object(index))
            .Where(reference => reference?.Object()?.Geometry is Brep)
            .Select(reference => reference!.ObjectId)
            .Distinct()
            .ToArray();
        if (!hadPreselectedObjects)
        {
            // As before: a preselection stays selected, panels picked at the prompt do not.
            foreach (Guid objectId in objectIds) document.Objects.Select(objectId, false);
        }
        return objectIds.Length == 0
            ? new TargetSelection(false, false, Array.Empty<Guid>(), PanelCladdingBakeScope.Surfaces,
                "No Brep panels were selected.")
            : TargetSelection.Ok(objectIds, (PanelCladdingBakeScope)scopeIndex);
    }

    private readonly record struct TargetSelection(
        bool Success,
        bool Cancelled,
        IReadOnlyList<Guid> ObjectIds,
        PanelCladdingBakeScope Scope,
        string Message)
    {
        public static TargetSelection Ok(IEnumerable<Guid> objectIds, PanelCladdingBakeScope scope) =>
            new(true, false, objectIds.Distinct().ToArray(), scope, string.Empty);
    }
}
