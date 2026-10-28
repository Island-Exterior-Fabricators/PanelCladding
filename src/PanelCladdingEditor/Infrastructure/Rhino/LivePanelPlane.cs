extern alias rhinocommon;

using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using BrepFace = rhinocommon::Rhino.Geometry.BrepFace;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using Plane = rhinocommon::Rhino.Geometry.Plane;
using PlaneFitResult = rhinocommon::Rhino.Geometry.PlaneFitResult;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace PanelCladdingEditor.Infrastructure.Rhino;

// The panel frame shared by every command (UD_PLANE). Reading prefers the stored UD_PLANE of the
// panel's object user text; otherwise, and for the auto value, the frame comes from the geometry:
// Z = the outward (front face) normal, Y = world up in the panel plane, X = Y x Z (right seen from
// outside), origin = lower-left corner of the extent.
internal static class LivePanelPlane
{
    internal static string? Stored(ObjectAttributes attributes)
    {
        var strings = attributes.GetUserStrings();
        string? key = strings?.AllKeys?.FirstOrDefault(item =>
            string.Equals(item, PanelCladdingKeyService.PlaneKey, StringComparison.OrdinalIgnoreCase));
        return key is null ? null : strings![key];
    }

    internal static string? Stored(IReadOnlyDictionary<string, string> userText) =>
        userText.FirstOrDefault(item =>
            string.Equals(item.Key, PanelCladdingKeyService.PlaneKey, StringComparison.OrdinalIgnoreCase)).Value;

    // Throws InvalidOperationException("PANEL_CLADDING_REFERENCE_PLANE_FAILED") as before.
    internal static (Plane Frame, string Diagnostic) Resolve(
        Brep brep, IReadOnlyList<Point3d> samples, double tolerance, string? stored)
    {
        if (PanelCladdingPlaneFormat.TryParse(stored, out PanelPlaneFrame frame))
        {
            var plane = new Plane(ToPoint(frame.Origin), ToVector(frame.XAxis), ToVector(frame.YAxis));
            if (plane.IsValid)
            {
                return (Orient(plane), "Frame: stored UD_PLANE.");
            }
        }
        return Compute(brep, samples, tolerance);
    }

    internal static (Plane Frame, string Diagnostic) Compute(Brep brep, IReadOnlyList<Point3d> samples, double tolerance)
    {
        Plane basis;
        Vector3d normal;
        string diagnostic;
        if (brep.Faces.Count > 0 && brep.Faces[0].TryGetPlane(out Plane planar, tolerance))
        {
            basis = planar;
            normal = planar.ZAxis;
            if (normal * FrontNormal(brep.Faces[0]) < 0d) normal.Reverse();
            diagnostic = "Frame: planar Brep face.";
        }
        else
        {
            PlaneFitResult fit = Plane.FitPlaneToPoints(samples, out Plane fitted);
            if (fit == PlaneFitResult.Failure || !fitted.IsValid)
            {
                throw new InvalidOperationException("PANEL_CLADDING_REFERENCE_PLANE_FAILED");
            }
            basis = fitted;
            normal = fitted.ZAxis;
            Vector3d front = Vector3d.Zero;
            foreach (BrepFace face in brep.Faces) front += FrontNormal(face);
            if (normal * front < 0d) normal.Reverse();
            diagnostic = "Frame: best-fit curved-panel plane.";
        }
        normal.Unitize();
        Vector3d up = Vector3d.ZAxis - (Vector3d.ZAxis * normal) * normal;
        if (!up.Unitize())
        {
            up = basis.YAxis - (basis.YAxis * normal) * normal;
            up.Unitize();
        }
        if (up * Vector3d.ZAxis < 0d) up.Reverse();
        Vector3d right = Vector3d.CrossProduct(up, normal);
        right.Unitize();
        var frame = new Plane(basis.Origin, right, up);
        if (!frame.IsValid || samples.Count == 0)
        {
            throw new InvalidOperationException("PANEL_CLADDING_REFERENCE_PLANE_FAILED");
        }
        double minX = double.MaxValue, minY = double.MaxValue;
        foreach (Point3d point in samples)
        {
            Vector3d delta = point - frame.Origin;
            minX = Math.Min(minX, delta * frame.XAxis);
            minY = Math.Min(minY, delta * frame.YAxis);
        }
        frame.Origin = frame.PointAt(minX, minY);
        return (frame, diagnostic);
    }

    // Gravity-up frame keeping the source's normal axis and X sign (a stored frame's handedness).
    internal static Plane Orient(Plane source)
    {
        Vector3d normal = source.ZAxis;
        normal.Unitize();
        Vector3d up = Vector3d.ZAxis - (Vector3d.ZAxis * normal) * normal;
        if (!up.Unitize())
        {
            up = source.YAxis;
            up.Unitize();
        }
        if (up * Vector3d.ZAxis < 0d)
        {
            up.Reverse();
        }
        Vector3d right = Vector3d.CrossProduct(up, normal);
        right.Unitize();
        Vector3d referenceRight = source.XAxis - (source.XAxis * normal) * normal - (source.XAxis * up) * up;
        if (referenceRight.Unitize() && right * referenceRight < 0d)
        {
            right.Reverse();
        }
        return new Plane(source.Origin, right, up);
    }

    internal static PanelPlaneFrame ToFrame(Plane plane) => new(
        new PanelPoint3(plane.Origin.X, plane.Origin.Y, plane.Origin.Z),
        new PanelPoint3(plane.XAxis.X, plane.XAxis.Y, plane.XAxis.Z),
        new PanelPoint3(plane.YAxis.X, plane.YAxis.Y, plane.YAxis.Z));

    private static Vector3d FrontNormal(BrepFace face)
    {
        Vector3d normal = face.NormalAt(face.Domain(0).Mid, face.Domain(1).Mid);
        if (face.OrientationIsReversed) normal.Reverse();
        normal.Unitize();
        return normal;
    }

    private static Point3d ToPoint(PanelPoint3 point) => new(point.X, point.Y, point.Z);

    private static Vector3d ToVector(PanelPoint3 vector) => new(vector.X, vector.Y, vector.Z);
}
