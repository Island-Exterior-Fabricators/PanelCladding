extern alias rhinocommon;

using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using Extrusion = rhinocommon::Rhino.Geometry.Extrusion;
using ObjectEnumeratorSettings = rhinocommon::Rhino.DocObjects.ObjectEnumeratorSettings;
using ObjectType = rhinocommon::Rhino.DocObjects.ObjectType;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using Surface = rhinocommon::Rhino.Geometry.Surface;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

// PCreport's read-only view of a model: layer paths and every Brep, surface, extrusion and curve
// with its object user text (case variants of a key kept apart). Nothing is written.
public static class LivePanelCladdingReportReader
{
    public static OperationResponse<PanelCladdingReportSnapshot> Read(RhinoDoc? document)
    {
        if (document is null)
        {
            return OperationResponse<PanelCladdingReportSnapshot>.Fail("NO_ACTIVE_DOCUMENT");
        }
        if (string.IsNullOrWhiteSpace(document.Path))
        {
            return OperationResponse<PanelCladdingReportSnapshot>.Fail("ACTIVE_DOC_UNSAVED");
        }

        var layerPaths = new Dictionary<int, string>();
        foreach (var layer in document.Layers)
        {
            if (!layer.IsDeleted)
            {
                layerPaths[layer.Index] = layer.FullPath ?? string.Empty;
            }
        }

        var objects = new List<PanelCladdingReportObject>();
        foreach (RhinoObject item in document.Objects.GetObjectList(new ObjectEnumeratorSettings
                 {
                     NormalObjects = true,
                     LockedObjects = true,
                     HiddenObjects = true,
                     ActiveObjects = true,
                     ReferenceObjects = false,
                     ObjectTypeFilter = ObjectType.Brep | ObjectType.Surface | ObjectType.Extrusion | ObjectType.Curve
                 }))
        {
            PanelCladdingReportObjectKind? kind = item.Geometry switch
            {
                Curve => PanelCladdingReportObjectKind.Curve,
                Brep or Surface or Extrusion => PanelCladdingReportObjectKind.Brep,
                _ => null
            };
            if (kind is null)
            {
                continue;
            }
            objects.Add(new PanelCladdingReportObject(
                item.Id,
                item.Attributes.Name ?? string.Empty,
                layerPaths.GetValueOrDefault(item.Attributes.LayerIndex, string.Empty),
                kind.Value,
                LivePanelCladdingCidService.Read(item.Attributes)));
        }

        return OperationResponse<PanelCladdingReportSnapshot>.Ok(new PanelCladdingReportSnapshot
        {
            DocumentPath = document.Path,
            Tolerance = document.ModelAbsoluteTolerance,
            LayerPaths = layerPaths.Values.ToArray(),
            Objects = objects
        });
    }
}
