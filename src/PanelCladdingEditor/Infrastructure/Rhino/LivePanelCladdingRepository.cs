extern alias rhinocommon;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Mesh = rhinocommon::Rhino.Geometry.Mesh;
using MeshingParameters = rhinocommon::Rhino.Geometry.MeshingParameters;
using Plane = rhinocommon::Rhino.Geometry.Plane;
using PlaneFitResult = rhinocommon::Rhino.Geometry.PlaneFitResult;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoMath = rhinocommon::Rhino.RhinoMath;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using UnitSystem = rhinocommon::Rhino.UnitSystem;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

public sealed partial class LivePanelCladdingRepository : ILivePanelCladdingRepository
{
    private readonly PanelCladdingKeyService _keys;
    private readonly PanelAxonometricProjectionService _projection;

    public LivePanelCladdingRepository(
        PanelCladdingKeyService keys,
        PanelAxonometricProjectionService projection)
    {
        _keys = keys;
        _projection = projection;
    }

    public OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId)
    {
        OperationResponse<RhinoDoc> resolved = ResolveDocument(filePath);
        return !resolved.Success || resolved.Data is null
            ? OperationResponse<PanelCladdingLayout>.Fail(resolved.Message)
            : ReadLayoutOnMainThread(resolved.Data, objectId);
    }

    public OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(
        string filePath,
        Guid objectId)
    {
        OperationResponse<RhinoDoc> resolved = ResolveDocument(filePath);
        return !resolved.Success || resolved.Data is null
            ? OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail(resolved.Message)
            : ReadMatchPanelOnMainThread(resolved.Data, objectId);
    }

    public OperationResponse<PanelAttributeCommitResult> CommitAttributes(
        PanelAttributeCommitRequest request,
        Func<OperationResponse> finalizeExternalCommit)
    {
        OperationResponse<RhinoDoc> resolved = ResolveDocument(request.FilePath);
        if (!resolved.Success || resolved.Data is null)
        {
            return OperationResponse<PanelAttributeCommitResult>.Fail(resolved.Message);
        }
        RhinoDoc document = resolved.Data;
        {
            RhinoObject? rhinoObject = document.Objects.FindId(request.ObjectId);
            if (rhinoObject?.Geometry is not Brep brep)
            {
                return OperationResponse<PanelAttributeCommitResult>.Fail("PANEL_CLADDING_BREP_NOT_FOUND");
            }

            string currentFingerprint = BuildFingerprint(rhinoObject, brep);
            if (!string.Equals(currentFingerprint, request.ExpectedGeometryFingerprint, StringComparison.Ordinal))
            {
                return OperationResponse<PanelAttributeCommitResult>.Fail(
                    "PANEL_CLADDING_STALE_EDITOR: panel geometry or attributes changed; reload before saving.");
            }

            var originalAttributes = rhinoObject.Attributes.Duplicate();
            var proposedAttributes = rhinoObject.Attributes.Duplicate();
            bool changed = false;
            var deleteKeys = request.UserTextDeletes
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            string?[] existingKeys = proposedAttributes.GetUserStrings()?.AllKeys ?? Array.Empty<string?>();
            foreach (string? key in existingKeys)
            {
                if (key is not null && deleteKeys.Contains(key))
                {
                    proposedAttributes.DeleteUserString(key);
                    changed = true;
                }
            }
            foreach ((string key, string value) in request.UserTextWrites)
            {
                if (!string.Equals(proposedAttributes.GetUserString(key), value, StringComparison.Ordinal))
                {
                    proposedAttributes.SetUserString(key, value);
                    changed = true;
                }
            }

            changed |= LivePanelCladdingCidService.Normalize(document, proposedAttributes);
            string? oldWorkbookPath = document.Strings.GetValue(PanelCladdingKeyService.WorkbookPathDocumentKey);
            bool workbookPathChanged = !string.IsNullOrWhiteSpace(request.WorkbookPath) &&
                !string.Equals(oldWorkbookPath, request.WorkbookPath, StringComparison.OrdinalIgnoreCase);

            if (!changed && !workbookPathChanged)
            {
                OperationResponse externalOnly = finalizeExternalCommit();
                return externalOnly.Success
                    ? OperationResponse<PanelAttributeCommitResult>.Ok(new PanelAttributeCommitResult
                    {
                        ObjectId = request.ObjectId,
                        Mutated = false
                    })
                    : OperationResponse<PanelAttributeCommitResult>.Fail(externalOnly.Message);
            }

            uint undoRecord = document.BeginUndoRecord("Save Panel Cladding Attributes");
            bool objectModified = false;
            try
            {
                if (changed)
                {
                    objectModified = document.Objects.ModifyAttributes(rhinoObject, proposedAttributes, quiet: true);
                    if (!objectModified)
                    {
                        return OperationResponse<PanelAttributeCommitResult>.Fail("PANEL_CLADDING_ATTRIBUTE_COMMIT_FAILED");
                    }
                }

                if (workbookPathChanged)
                {
                    document.Strings.SetString(PanelCladdingKeyService.WorkbookPathDocumentKey, request.WorkbookPath!);
                }

                OperationResponse external = finalizeExternalCommit();
                if (!external.Success)
                {
                    if (objectModified)
                    {
                        RhinoObject? changedObject = document.Objects.FindId(request.ObjectId);
                        if (changedObject is not null)
                        {
                            document.Objects.ModifyAttributes(changedObject, originalAttributes, quiet: true);
                        }
                    }
                    RestoreDocumentString(document, oldWorkbookPath);
                    return OperationResponse<PanelAttributeCommitResult>.Fail(external.Message);
                }

                document.Views.Redraw();
                return OperationResponse<PanelAttributeCommitResult>.Ok(new PanelAttributeCommitResult
                {
                    ObjectId = request.ObjectId,
                    Mutated = changed || workbookPathChanged
                });
            }
            finally
            {
                if (undoRecord != 0U)
                {
                    document.EndUndoRecord(undoRecord);
                }
            }
        }
    }

    public OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath)
    {
        if (string.IsNullOrWhiteSpace(workbookPath))
        {
            return OperationResponse<string>.Fail("PANEL_CLADDING_WORKBOOK_PATH_REQUIRED");
        }
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(workbookPath);
        }
        catch (Exception ex)
        {
            return OperationResponse<string>.Fail($"PANEL_CLADDING_WORKBOOK_PATH_INVALID: {ex.Message}");
        }
        if (!string.Equals(Path.GetExtension(fullPath), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return OperationResponse<string>.Fail("PANEL_CLADDING_WORKBOOK_XLSX_REQUIRED");
        }

        OperationResponse<RhinoDoc> resolved = ResolveDocument(filePath);
        if (!resolved.Success || resolved.Data is null)
        {
            return OperationResponse<string>.Fail(resolved.Message);
        }
        resolved.Data.Strings.SetString(PanelCladdingKeyService.WorkbookPathDocumentKey, fullPath);
        return OperationResponse<string>.Ok(fullPath);
    }

    private static OperationResponse<RhinoDoc> ResolveDocument(string filePath)
    {
        RhinoDoc? document = RhinoDoc.ActiveDoc;
        if (document is null)
        {
            return OperationResponse<RhinoDoc>.Fail("NO_ACTIVE_DOCUMENT");
        }
        if (string.IsNullOrWhiteSpace(document.Path))
        {
            return OperationResponse<RhinoDoc>.Fail("ACTIVE_DOC_UNSAVED");
        }
        try
        {
            if (!string.Equals(
                Path.GetFullPath(document.Path).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(filePath).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
            {
                return OperationResponse<RhinoDoc>.Fail("FILE_NOT_ACTIVE");
            }
        }
        catch (Exception ex)
        {
            return OperationResponse<RhinoDoc>.Fail($"DOCUMENT_PATH_INVALID: {ex.Message}");
        }
        return OperationResponse<RhinoDoc>.Ok(document);
    }

    private OperationResponse<PanelCladdingLayout> ReadLayoutOnMainThread(RhinoDoc document, Guid objectId)
    {
        RhinoObject? rhinoObject = document.Objects.FindId(objectId);
        if (rhinoObject?.Geometry is not Brep brep)
        {
            return OperationResponse<PanelCladdingLayout>.Fail("PANEL_CLADDING_BREP_NOT_FOUND");
        }
        OperationResponse frameKeys = ValidateFrameAttributeNames(rhinoObject);
        if (!frameKeys.Success) return OperationResponse<PanelCladdingLayout>.Fail(frameKeys.Message);

        OperationResponse<LocalMesh> localMeshResponse = BuildLocalMesh(
            brep, document.ModelAbsoluteTolerance, LivePanelPlane.Stored(rhinoObject.Attributes));
        if (!localMeshResponse.Success || localMeshResponse.Data is null)
        {
            return OperationResponse<PanelCladdingLayout>.Fail(localMeshResponse.Message);
        }
        LocalMesh local = localMeshResponse.Data;
        double width = local.XMax - local.XMin;
        double height = local.YMax - local.YMin;
        if (width <= document.ModelAbsoluteTolerance || height <= document.ModelAbsoluteTolerance)
        {
            return OperationResponse<PanelCladdingLayout>.Fail("PANEL_CLADDING_PANEL_EXTENT_INVALID");
        }

        IReadOnlyDictionary<string, string> userText = ReadUserText(rhinoObject);
        OperationResponse<PanelCladdingKeySet> keys = _keys.Parse(
            userText,
            width,
            height,
            document.ModelAbsoluteTolerance);
        if (!keys.Success || keys.Data is null)
        {
            return OperationResponse<PanelCladdingLayout>.Fail(keys.Message);
        }

        OperationResponse<(PanelGeometryClass Classification, string Diagnostic, PanelPreviewGeometry Preview)> preview =
            _projection.Build(
                local.Vertices,
                local.Triangles,
                local.XMin,
                local.XMax,
                local.YMin,
                local.YMax,
                keys.Data.HorizontalOffsets,
                keys.Data.VerticalOffsets,
                keys.Data.Cells,
                document.ModelAbsoluteTolerance);
        if (!preview.Success)
        {
            return OperationResponse<PanelCladdingLayout>.Fail(preview.Message);
        }

        var previewData = preview.Data;
        string layerPath = rhinoObject.Attributes.LayerIndex >= 0
            ? document.Layers[rhinoObject.Attributes.LayerIndex]?.FullPath ?? string.Empty
            : string.Empty;
        string systemCode = layerPath.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault() ?? "PANEL";
        return OperationResponse<PanelCladdingLayout>.Ok(new PanelCladdingLayout
        {
            ObjectId = objectId,
            DocumentRuntimeSerialNumber = document.RuntimeSerialNumber,
            DocumentPath = document.Path,
            ObjectName = rhinoObject.Attributes.Name ?? string.Empty,
            LayerFullPath = layerPath,
            SystemCode = systemCode,
            GeometryFingerprint = BuildFingerprint(rhinoObject, brep),
            GeometryClass = previewData.Classification,
            GeometryDiagnostic = $"{local.FrameDiagnostic} {previewData.Diagnostic}".Trim(),
            Width = width,
            Height = height,
            ModelTolerance = document.ModelAbsoluteTolerance,
            ModelUnitScaleToMillimeters = RhinoMath.UnitScale(document.ModelUnitSystem, UnitSystem.Millimeters),
            HorizontalOffsets = keys.Data.HorizontalOffsets,
            VerticalOffsets = keys.Data.VerticalOffsets,
            Cells = keys.Data.Cells,
            Topology = keys.Data.Topology,
            FrameAssignments = keys.Data.FrameAssignments,
            SourceUserText = userText,
            Preview = previewData.Preview,
            WorkbookPath = document.Strings.GetValue(PanelCladdingKeyService.WorkbookPathDocumentKey) ?? string.Empty
        });
    }

    private OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanelOnMainThread(
        RhinoDoc document,
        Guid objectId)
    {
        RhinoObject? rhinoObject = document.Objects.FindId(objectId);
        if (rhinoObject?.Geometry is not Brep brep)
        {
            return OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail(
                "PANEL_CLADDING_BREP_NOT_FOUND");
        }

        OperationResponse<LocalMesh> localMeshResponse = BuildLocalMesh(
            brep,
            document.ModelAbsoluteTolerance,
            LivePanelPlane.Stored(rhinoObject.Attributes));
        if (!localMeshResponse.Success || localMeshResponse.Data is null)
        {
            return OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail(localMeshResponse.Message);
        }
        LocalMesh local = localMeshResponse.Data;
        double width = local.XMax - local.XMin;
        double height = local.YMax - local.YMin;
        if (width <= document.ModelAbsoluteTolerance || height <= document.ModelAbsoluteTolerance)
        {
            return OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail(
                "PANEL_CLADDING_PANEL_EXTENT_INVALID");
        }

        var geometryCell = new PanelCladdingCell
        {
            Column = 0,
            Row = 0,
            RowLabel = "A",
            ShortLabel = "0A",
            UserTextKey = PanelCladdingKeyService.GetCellKey(0, "A"),
            Value = string.Empty
        };
        OperationResponse<(PanelGeometryClass Classification, string Diagnostic, PanelPreviewGeometry Preview)> preview =
            _projection.Build(
                local.Vertices,
                local.Triangles,
                local.XMin,
                local.XMax,
                local.YMin,
                local.YMax,
                Array.Empty<double>(),
                Array.Empty<double>(),
                new[] { geometryCell },
                document.ModelAbsoluteTolerance);
        if (!preview.Success)
        {
            return OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail(preview.Message);
        }

        string layerPath = rhinoObject.Attributes.LayerIndex >= 0
            ? document.Layers[rhinoObject.Attributes.LayerIndex]?.FullPath ?? string.Empty
            : string.Empty;
        string systemCode = layerPath.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault() ?? "PANEL";
        OperationResponse frameKeys = ValidateFrameAttributeNames(rhinoObject);
        if (!frameKeys.Success) return OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail(frameKeys.Message);
        return OperationResponse<PanelCladdingMatchPanelSnapshot>.Ok(
            new PanelCladdingMatchPanelSnapshot
            {
                ObjectId = objectId,
                SystemCode = systemCode,
                Geometry = new PanelCladdingMatchGeometryDescriptor
                {
                    GeometryClass = preview.Data.Classification,
                    Width = width,
                    Height = height,
                    ModelTolerance = document.ModelAbsoluteTolerance,
                    DepthSamples = preview.Data.Preview.DepthSamples
                },
                UserText = ReadUserText(rhinoObject)
            });
    }

    // The editor layout's width and height (render-mesh extents in the panel frame) and the frame
    // itself, computed from the geometry for the auto attribute refresh, so CW_2.00-2.02 match what
    // PCEditor and PCSyncSrf write and UD_PLANE follows the panel.
    internal static OperationResponse<(double Width, double Height, PanelPlaneFrame Plane)> MeasurePanel(Brep brep, double tolerance)
    {
        OperationResponse<LocalMesh> local;
        try
        {
            local = BuildLocalMesh(brep, tolerance, storedPlane: null);
        }
        catch (InvalidOperationException exception)
        {
            return OperationResponse<(double Width, double Height, PanelPlaneFrame Plane)>.Fail(exception.Message);
        }
        if (!local.Success || local.Data is null)
        {
            return OperationResponse<(double Width, double Height, PanelPlaneFrame Plane)>.Fail(local.Message);
        }
        double width = local.Data.XMax - local.Data.XMin;
        double height = local.Data.YMax - local.Data.YMin;
        return width <= tolerance || height <= tolerance
            ? OperationResponse<(double Width, double Height, PanelPlaneFrame Plane)>.Fail("PANEL_CLADDING_PANEL_EXTENT_INVALID")
            : OperationResponse<(double Width, double Height, PanelPlaneFrame Plane)>.Ok((width, height, local.Data.Frame));
    }

    private static OperationResponse<LocalMesh> BuildLocalMesh(Brep brep, double tolerance, string? storedPlane)
    {
        Mesh[] pieces = Mesh.CreateFromBrep(brep, MeshingParameters.FastRenderMesh);
        if (pieces.Length == 0)
        {
            pieces = Mesh.CreateFromBrep(brep, MeshingParameters.Default);
        }
        if (pieces.Length == 0)
        {
            return OperationResponse<LocalMesh>.Fail("PANEL_CLADDING_PREVIEW_MESH_FAILED");
        }

        var combined = new Mesh();
        combined.Append(pieces);
        combined.Compact();
        if (combined.Vertices.Count < 3 || combined.Faces.Count == 0)
        {
            return OperationResponse<LocalMesh>.Fail("PANEL_CLADDING_PREVIEW_MESH_EMPTY");
        }
        if (combined.Faces.Count > 100_000)
        {
            return OperationResponse<LocalMesh>.Fail("PANEL_CLADDING_PREVIEW_MESH_TOO_DENSE");
        }

        (Plane Frame, string Diagnostic) frame = LivePanelPlane.Resolve(
            brep, combined.Vertices.Select(vertex => new Point3d(vertex)).ToArray(), tolerance, storedPlane);
        var vertices = new List<PanelPoint3>(combined.Vertices.Count);
        foreach (var vertex in combined.Vertices)
        {
            var point = new Point3d(vertex);
            Vector3d delta = point - frame.Frame.Origin;
            vertices.Add(new PanelPoint3(
                delta * frame.Frame.XAxis,
                delta * frame.Frame.YAxis,
                delta * frame.Frame.ZAxis));
        }

        var triangles = new List<PanelTriangle>(combined.Faces.Count * 2);
        foreach (var face in combined.Faces)
        {
            triangles.Add(new PanelTriangle(face.A, face.B, face.C));
            if (face.IsQuad)
            {
                triangles.Add(new PanelTriangle(face.A, face.C, face.D));
            }
        }
        return OperationResponse<LocalMesh>.Ok(new LocalMesh
        {
            Vertices = vertices,
            Triangles = triangles,
            XMin = vertices.Min(point => point.X),
            XMax = vertices.Max(point => point.X),
            YMin = vertices.Min(point => point.Y),
            YMax = vertices.Max(point => point.Y),
            Frame = LivePanelPlane.ToFrame(frame.Frame),
            FrameDiagnostic = frame.Diagnostic
        });
    }

    private static OperationResponse ValidateFrameAttributeNames(RhinoObject rhinoObject)
    {
        var strings = rhinoObject.Attributes.GetUserStrings();
        var exactKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string? key in strings?.AllKeys ?? Array.Empty<string?>())
        {
            if (key is not null) exactKeys[key] = strings![key] ?? string.Empty;
        }
        return PanelCladdingKeyService.ValidateFrameAttributeNames(exactKeys);
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

    private static string BuildFingerprint(RhinoObject rhinoObject, Brep brep)
    {
        var payload = new StringBuilder();
        payload.Append(brep.DataCRC(0U).ToString(CultureInfo.InvariantCulture));
        payload.Append('|').Append(rhinoObject.Attributes.LayerIndex.ToString(CultureInfo.InvariantCulture));
        var strings = ReadUserText(rhinoObject);
        foreach ((string key, string value) in strings.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (key.StartsWith("CW_2.", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("CW_4.", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("UD_", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("CW_9.", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, PanelCladdingKeyService.FrameConfigKey, StringComparison.OrdinalIgnoreCase))
            {
                payload.Append('|').Append(key).Append('=').Append(value);
            }
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload.ToString()))).ToLowerInvariant();
    }

    private static void RestoreDocumentString(RhinoDoc document, string? oldValue)
    {
        if (oldValue is null)
        {
            document.Strings.Delete(PanelCladdingKeyService.WorkbookPathDocumentKey);
        }
        else
        {
            document.Strings.SetString(PanelCladdingKeyService.WorkbookPathDocumentKey, oldValue);
        }
    }

    private sealed class LocalMesh
    {
        public IReadOnlyList<PanelPoint3> Vertices { get; init; } = Array.Empty<PanelPoint3>();
        public IReadOnlyList<PanelTriangle> Triangles { get; init; } = Array.Empty<PanelTriangle>();
        public double XMin { get; init; }
        public double XMax { get; init; }
        public double YMin { get; init; }
        public double YMax { get; init; }
        public PanelPlaneFrame Frame { get; init; }
        public string FrameDiagnostic { get; init; } = string.Empty;
    }
}

