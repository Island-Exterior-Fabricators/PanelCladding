extern alias rhinocommon;

using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Layer = rhinocommon::Rhino.DocObjects.Layer;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectEnumeratorSettings = rhinocommon::Rhino.DocObjects.ObjectEnumeratorSettings;
using ObjectType = rhinocommon::Rhino.DocObjects.ObjectType;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using TextDot = rhinocommon::Rhino.Geometry.TextDot;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

internal static class LivePanelCladdingBakeIssueDots
{
    internal static OperationResponse Mark(
        RhinoDoc document,
        IReadOnlyList<Guid> panelObjectIds,
        IReadOnlyList<PanelCladdingBakeIssue> issues,
        ref bool documentMutationOccurred)
    {
        var panels = panelObjectIds.ToHashSet();
        RhinoObject[] stale = document.Objects.GetObjectList(new ObjectEnumeratorSettings
            {
                NormalObjects = true,
                LockedObjects = true,
                HiddenObjects = true,
                ActiveObjects = true,
                ReferenceObjects = false,
                ObjectTypeFilter = ObjectType.TextDot
            })
            .Where(dot => PanelCladdingBakeIssueService.IsDotOf(
                dot.Attributes.GetUserString(PanelCladdingBakeIssueService.PanelUserTextKey), panels))
            .ToArray();
        foreach (RhinoObject dot in stale)
        {
            if (!document.Objects.Delete(dot, quiet: true, ignoreModes: true))
            {
                return OperationResponse.Fail($"PANEL_CLADDING_BAKE_ISSUE_DOT_DELETE_FAILED: {dot.Id:D}");
            }
            documentMutationOccurred = true;
        }

        var located = issues
            .Select(issue => (Issue: issue, Panel: document.Objects.FindId(issue.PanelObjectId)?.Geometry as Brep))
            .Where(item => item.Panel is not null)
            .ToArray();
        if (located.Length == 0)
        {
            return OperationResponse.Ok();
        }
        int layerIndex = EnsureIssueLayer(document, ref documentMutationOccurred);
        if (layerIndex < 0)
        {
            return OperationResponse.Fail(
                $"PANEL_CLADDING_LAYER_CREATE_FAILED: {PanelCladdingBakeIssueService.IssueLayerPath}");
        }
        foreach ((PanelCladdingBakeIssue issue, Brep? panel) in located)
        {
            var attributes = new ObjectAttributes { LayerIndex = layerIndex, Name = "PCbake issue" };
            attributes.SetUserString(PanelCladdingBakeIssueService.PanelUserTextKey, issue.PanelObjectId.ToString("D"));
            attributes.SetUserString(PanelCladdingBakeIssueService.DetailUserTextKey, $"{issue.Stage}: {issue.Detail}");
            using var dot = new TextDot(
                PanelCladdingBakeIssueService.DotText(issue),
                panel!.GetBoundingBox(accurate: true).Center);
            if (document.Objects.AddTextDot(dot, attributes) == Guid.Empty)
            {
                return OperationResponse.Fail($"PANEL_CLADDING_BAKE_ISSUE_DOT_FAILED: {issue.PanelObjectId:D}");
            }
            documentMutationOccurred = true;
        }
        return OperationResponse.Ok();
    }

    private static int EnsureIssueLayer(RhinoDoc document, ref bool documentMutationOccurred)
    {
        int index = document.Layers.FindByFullPath(PanelCladdingBakeIssueService.IssueLayerPath, -1);
        if (index >= 0)
        {
            return index;
        }
        PanelColorRgb color = PanelCladdingBakeIssueService.IssueLayerColor;
        index = document.Layers.Add(new Layer
        {
            Name = PanelCladdingBakeIssueService.IssueLayerPath,
            Color = System.Drawing.Color.FromArgb(color.Red, color.Green, color.Blue)
        });
        documentMutationOccurred |= index >= 0;
        return index;
    }
}
