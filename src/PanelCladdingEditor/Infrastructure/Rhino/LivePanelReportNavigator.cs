extern alias rhinocommon;

using PanelCladdingEditor.Application.Interfaces;
using BoundingBox = rhinocommon::Rhino.Geometry.BoundingBox;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace PanelCladdingEditor.Infrastructure.Rhino;

/// <summary>
/// Called by the modeless report window on Rhino's UI thread. Changes only the selection and the
/// active view; locked or hidden objects are zoomed to but cannot be selected.
/// </summary>
public sealed class LivePanelReportNavigator : IPanelReportNavigator
{
    private readonly uint _documentSerial;

    public LivePanelReportNavigator(uint documentRuntimeSerialNumber)
    {
        _documentSerial = documentRuntimeSerialNumber;
    }

    public void Show(IReadOnlyCollection<Guid> objectIds)
    {
        RhinoDoc? document = RhinoDoc.FromRuntimeSerialNumber(_documentSerial);
        if (document is null)
        {
            return;
        }
        RhinoObject[] objects = objectIds
            .Where(id => id != Guid.Empty)
            .Select(id => document.Objects.FindId(id))
            .OfType<RhinoObject>()
            .ToArray();
        document.Objects.UnselectAll();
        if (objects.Length == 0)
        {
            document.Views.Redraw();
            return;
        }
        BoundingBox box = BoundingBox.Empty;
        foreach (RhinoObject item in objects)
        {
            item.Select(true);
            box.Union(item.Geometry.GetBoundingBox(true));
        }
        if (box.IsValid && document.Views.ActiveView is { } view)
        {
            box.Inflate(Math.Max(box.Diagonal.Length * 0.15d, document.ModelAbsoluteTolerance * 100d));
            view.ActiveViewport.ZoomBoundingBox(box);
        }
        document.Views.Redraw();
    }
}
