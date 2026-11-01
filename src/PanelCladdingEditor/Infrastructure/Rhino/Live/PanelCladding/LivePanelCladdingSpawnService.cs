extern alias rhinocommon;

using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using Layer = rhinocommon::Rhino.DocObjects.Layer;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

public sealed partial class LivePanelCladdingSpawnService
{
    private readonly ILivePanelCladdingRepository _layoutRepository;
    private readonly PanelCladdingKeyService _keys;
    private readonly PanelCladdingSpawnPlanningService _planning;
    private readonly PanelCladdingExtrusionPlanningService _extrusionPlanning = new();

    public LivePanelCladdingSpawnService(
        ILivePanelCladdingRepository layoutRepository,
        PanelCladdingKeyService keys,
        PanelCladdingSpawnPlanningService planning)
    {
        _layoutRepository = layoutRepository;
        _keys = keys;
        _planning = planning;
    }

    internal OperationResponse<PreparedPanel> PreparePanel(
        RhinoDoc document,
        string filePath,
        Guid objectId,
        PanelCladdingObjectScope scope,
        bool allowEmptySurfacePlan = false)
    {
        RhinoObject? sourceObject = document.Objects.FindId(objectId);
        if (sourceObject?.Geometry is not Brep sourceBrep)
        {
            return OperationResponse<PreparedPanel>.Fail("PANEL_CLADDING_BREP_NOT_FOUND");
        }

        OperationResponse<PanelCladdingLayout> layoutResponse = _layoutRepository.ReadLayout(filePath, objectId);
        if (!layoutResponse.Success || layoutResponse.Data is null)
        {
            return OperationResponse<PreparedPanel>.Fail(layoutResponse.Message);
        }
        PanelCladdingLayout layout = layoutResponse.Data;
        if (!layout.CanSave)
        {
            return OperationResponse<PreparedPanel>.Fail(
                $"PANEL_CLADDING_UNSUPPORTED_PROJECTION: {layout.GeometryDiagnostic}");
        }

        IReadOnlyDictionary<string, string> userText = ReadUserText(sourceObject);
        OperationResponse<PanelCladdingKeySet> keySetResponse = _keys.Parse(
            userText,
            layout.Width,
            layout.Height,
            layout.ModelTolerance);
        if (!keySetResponse.Success || keySetResponse.Data is null)
        {
            return OperationResponse<PreparedPanel>.Fail(keySetResponse.Message);
        }
        PanelCladdingKeySet keySet = keySetResponse.Data;

        PanelCladdingSpawnPlan plan;
        if (scope == PanelCladdingObjectScope.Surfaces)
        {
            OperationResponse<PanelCladdingSpawnPlan> planResponse = allowEmptySurfacePlan
                ? _planning.CreateUpdatePlan(userText, keySet)
                : _planning.CreatePlan(userText, keySet);
            if (!planResponse.Success || planResponse.Data is null)
            {
                return OperationResponse<PreparedPanel>.Fail(planResponse.Message);
            }
            plan = planResponse.Data;
        }
        else
        {
            string pid = GetRequiredUserText(
                userText,
                PanelCladdingSpawnPlanningService.PanelIdUserTextKey);
            string cid = GetRequiredUserText(
                userText,
                PanelCladdingSpawnPlanningService.CidUserTextKey);
            if (PanelCladdingCidService.PanelCidWrite(userText) is not null)
            {
                cid = PanelCladdingCidService.ResolvePanelCid(pid, userText);
            }
            if (pid.Length == 0 || cid.Length == 0)
            {
                return OperationResponse<PreparedPanel>.Fail(
                    "PANEL_CLADDING_EXTRUSION_PID_CID_REQUIRED");
            }
            OperationResponse<IReadOnlyList<PanelCladdingExtrusionCurvePlan>> curvePlan =
                _extrusionPlanning.CreatePlan(
                    pid,
                    cid,
                    layout.Width,
                    layout.Height,
                    keySet,
                    layout.LayerFullPath,
                    GetRequiredUserText(userText, PanelCladdingSpawnPlanningService.ReleaseUserTextKey),
                    PanelCladdingProjectService.CodeFromDocumentPath(document.Path));
            if (!curvePlan.Success || curvePlan.Data is null)
            {
                return OperationResponse<PreparedPanel>.Fail(curvePlan.Message);
            }
            plan = new PanelCladdingSpawnPlan
            {
                PanelId = pid,
                Curves = curvePlan.Data
            };
        }

        OperationResponse<LivePanelCladdingGeometryGrid> gridResponse =
            LivePanelCladdingGeometryPartitionService.CreateGrid(
                sourceBrep,
                keySet,
                layout.ModelTolerance,
                LivePanelPlane.Stored(userText));
        if (!gridResponse.Success || gridResponse.Data is null)
        {
            return OperationResponse<PreparedPanel>.Fail(gridResponse.Message);
        }
        using LivePanelCladdingGeometryGrid grid = gridResponse.Data;
        var prepared = new List<PreparedRegion>(plan.Regions.Count);
        foreach (PanelCladdingSpawnRegionPlan region in plan.Regions)
        {
            OperationResponse<Brep> geometry = LivePanelCladdingGeometryPartitionService.CreateRegionSurface(
                grid,
                region.Cells,
                layout.ModelTolerance,
                region.Cid);
            if (!geometry.Success || geometry.Data is null)
            {
                foreach (PreparedRegion item in prepared)
                {
                    item.Geometry.Dispose();
                }
                return OperationResponse<PreparedPanel>.Fail(geometry.Message);
            }
            prepared.Add(new PreparedRegion(objectId, region, geometry.Data));
        }
        var curves = new List<PreparedCurve>(plan.Curves.Count);
        foreach (PanelCladdingExtrusionCurvePlan curvePlan in plan.Curves)
        {
            OperationResponse<Curve> geometry =
                LivePanelCladdingGeometryPartitionService.CreateExtrusionCurve(grid, curvePlan);
            if (!geometry.Success || geometry.Data is null)
            {
                foreach (PreparedRegion item in prepared)
                {
                    item.Geometry.Dispose();
                }
                foreach (PreparedCurve item in curves)
                {
                    item.Geometry.Dispose();
                }
                return OperationResponse<PreparedPanel>.Fail(geometry.Message);
            }
            curves.Add(new PreparedCurve(objectId, curvePlan, geometry.Data));
        }
        return OperationResponse<PreparedPanel>.Ok(new PreparedPanel(prepared, curves));
    }

    internal static OperationResponse<int> EnsureLayer(RhinoDoc document, string layerPath)
    {
        int layerIndex = document.Layers.FindByFullPath(layerPath, -1);
        if (layerIndex < 0)
        {
            layerIndex = document.Layers.AddPath(layerPath);
        }
        return layerIndex < 0
            ? OperationResponse<int>.Fail($"PANEL_CLADDING_LAYER_CREATE_FAILED: {layerPath}")
            : OperationResponse<int>.Ok(layerIndex);
    }

    internal static OperationResponse<int> EnsureMaterialLayer(
        RhinoDoc document,
        PanelCladdingSpawnRegionPlan cell)
    {
        int layerIndex = document.Layers.FindByFullPath(cell.LayerPath, -1);
        if (layerIndex < 0)
        {
            layerIndex = document.Layers.AddPath(cell.LayerPath);
        }
        if (layerIndex < 0)
        {
            return OperationResponse<int>.Fail(
                $"PANEL_CLADDING_LAYER_CREATE_FAILED: {cell.LayerPath}");
        }

        Layer? layer = document.Layers[layerIndex];
        if (layer is null)
        {
            return OperationResponse<int>.Fail(
                $"PANEL_CLADDING_LAYER_NOT_FOUND_AFTER_CREATE: {cell.LayerPath}");
        }
        var expectedColor = System.Drawing.Color.FromArgb(
            cell.LayerColor.Red,
            cell.LayerColor.Green,
            cell.LayerColor.Blue);
        if (layer.Color.ToArgb() != expectedColor.ToArgb())
        {
            layer.Color = expectedColor;
            if (!document.Layers.Modify(layer, layerIndex, quiet: true))
            {
                return OperationResponse<int>.Fail(
                    $"PANEL_CLADDING_LAYER_COLOR_FAILED: {cell.LayerPath}");
            }
        }
        return OperationResponse<int>.Ok(layerIndex);
    }

    private static IReadOnlyDictionary<string, string> ReadUserText(RhinoObject rhinoObject)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var strings = rhinoObject.Attributes.GetUserStrings();
        if (strings?.AllKeys is null)
        {
            return result;
        }
        foreach (string? key in strings.AllKeys)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = strings[key] ?? string.Empty;
            }
        }
        return result;
    }

    private static string GetRequiredUserText(
        IReadOnlyDictionary<string, string> userText,
        string key) => userText.TryGetValue(key, out string? value)
            ? (value ?? string.Empty).Trim()
            : string.Empty;

    internal sealed record PreparedRegion(
        Guid SourcePanelId,
        PanelCladdingSpawnRegionPlan Region,
        Brep Geometry);

    internal sealed record PreparedCurve(
        Guid SourcePanelId,
        PanelCladdingExtrusionCurvePlan CurvePlan,
        Curve Geometry);

    internal sealed record PreparedPanel(
        IReadOnlyList<PreparedRegion> Regions,
        IReadOnlyList<PreparedCurve> Curves);

}
