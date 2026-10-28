using System.Globalization;
using System.Text.RegularExpressions;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

// UD_PLANE text: "{ox, oy, oz};{xx,xy,xz};{yx,yy,yz}" (the form already in project models).
// Z = X x Y points out of the facade, Y is up and X is right seen from outside; the origin is the
// lower-left corner of the panel. Reading takes the first nine numbers, as before.
public static partial class PanelCladdingPlaneFormat
{
    public const int Decimals = 6;
    // 1 - dot of a stored axis and the computed one; formatting rounding stays far below it.
    public const double AxisTolerance = 1e-9;

    public static string Format(PanelPlaneFrame frame) =>
        $"{{{Number(frame.Origin.X)}, {Number(frame.Origin.Y)}, {Number(frame.Origin.Z)}}};" +
        $"{{{Number(frame.XAxis.X)},{Number(frame.XAxis.Y)},{Number(frame.XAxis.Z)}}};" +
        $"{{{Number(frame.YAxis.X)},{Number(frame.YAxis.Y)},{Number(frame.YAxis.Z)}}}";

    // False when blank, short of nine numbers, or an axis has no length.
    public static bool TryParse(string? text, out PanelPlaneFrame frame)
    {
        frame = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        double[] values = NumberRegex().Matches(text)
            .Select(match => double.Parse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture))
            .ToArray();
        if (values.Length < 9 || values.Take(9).Any(value => !double.IsFinite(value))) return false;
        if (!TryUnitize(new PanelPoint3(values[3], values[4], values[5]), out PanelPoint3 x) ||
            !TryUnitize(new PanelPoint3(values[6], values[7], values[8]), out PanelPoint3 y))
        {
            return false;
        }
        frame = new PanelPlaneFrame(new PanelPoint3(values[0], values[1], values[2]), x, y);
        return true;
    }

    // The stored text describes the desired frame: origins within the tolerance, axes parallel.
    public static bool Matches(string? stored, PanelPlaneFrame desired, double tolerance)
    {
        if (!TryParse(stored, out PanelPlaneFrame current)) return false;
        double dx = current.Origin.X - desired.Origin.X;
        double dy = current.Origin.Y - desired.Origin.Y;
        double dz = current.Origin.Z - desired.Origin.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz) <= Math.Max(tolerance, 0) &&
            1 - Dot(current.XAxis, desired.XAxis) <= AxisTolerance &&
            1 - Dot(current.YAxis, desired.YAxis) <= AxisTolerance;
    }

    private static string Number(double value)
    {
        double rounded = Math.Round(value, Decimals, MidpointRounding.AwayFromZero);
        return (rounded == 0 ? 0 : rounded).ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static bool TryUnitize(PanelPoint3 vector, out PanelPoint3 unit)
    {
        double length = Math.Sqrt(Dot(vector, vector));
        unit = length > 1e-12 && double.IsFinite(length)
            ? new PanelPoint3(vector.X / length, vector.Y / length, vector.Z / length)
            : default;
        return length > 1e-12 && double.IsFinite(length);
    }

    private static double Dot(PanelPoint3 a, PanelPoint3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    [GeneratedRegex(@"[-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();
}
