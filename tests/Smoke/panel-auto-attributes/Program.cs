using PanelCladdingEditor.Application.Services.PanelCladding;

namespace PanelAutoAttributesSmoke;

internal static class Program
{
    private const double SquareInchesPerSquareFoot = 144d;
    private const double SquareMillimetresPerSquareFoot = 304.8d * 304.8d;

    private static void Main()
    {
        VerifyUserExample();
        VerifyPsf();
        VerifyChanges();
        VerifyInputKeys();
        Console.WriteLine("PASS: panel auto attribute regression");
    }

    // The user's example: a 90 x 305 in panel with CW_6.01_PSF = 85.
    private static void VerifyUserExample()
    {
        var values = PanelCladdingAutoAttributeService.CreateValues(
            90, 305, 90 * 305 / SquareInchesPerSquareFoot, new Dictionary<string, string> { ["CW_6.01_PSF"] = "85" });
        Require(values.Count == 5 &&
                values["CW_2.00_UNIT_DIMENSION"] == "90.00000x305.00000" &&
                values["CW_2.01_UNIT_WIDTH"] == "90.00000" &&
                values["CW_2.02_UNIT_HEIGHT"] == "305.00000" &&
                values["CW_2.07_UNIT_AREA"] == "190.625 sqf" &&
                values["CW_6.02_UNIT_WEIGHT"] == "16203.125",
            "The 90 x 305 in, PSF 85 panel gives the user's five values: " + string.Join("; ", values));

        // A millimetre model: 2286 x 7747 mm is the same panel; the area is still square feet.
        var metric = PanelCladdingAutoAttributeService.CreateValues(
            2286, 7747, 2286d * 7747d / SquareMillimetresPerSquareFoot, new Dictionary<string, string> { ["cw_6.01_psf"] = "85 psf" });
        Require(metric["CW_2.00_UNIT_DIMENSION"] == "2286.00000x7747.00000" &&
                metric["CW_2.07_UNIT_AREA"] == "190.625 sqf" && metric["CW_6.02_UNIT_WEIGHT"] == "16203.125",
            "Millimetre dimensions stay in model units; area converts to square feet.");

        // Weight uses the unrounded area: 12.5 x 190.6254 = 2382.8175.
        var rounded = PanelCladdingAutoAttributeService.CreateValues(
            90, 305, 190.6254, new Dictionary<string, string> { ["CW_6.01_PSF"] = "12.5" });
        Require(rounded["CW_2.07_UNIT_AREA"] == "190.625 sqf" && rounded["CW_6.02_UNIT_WEIGHT"] == "2382.818",
            "Area and weight use three decimals; weight multiplies the unrounded area.");
        Console.WriteLine("[OK] 90x305 in, PSF 85 -> 90.00000x305.00000, 190.625 sqf, 16203.125 (mm models convert the area)");
    }

    private static void VerifyPsf()
    {
        foreach ((string text, double expected) in new[] { ("85", 85d), (" 85 psf", 85d), ("12.5PSF", 12.5d), (".5", 0.5d), ("0", 0d) })
        {
            Require(PanelCladdingAutoAttributeService.TryParsePsf(text, out double psf) && psf == expected, $"PSF '{text}' parses as {expected}.");
        }
        foreach (string? text in new[] { null, "", " ", "TBD", "psf 85", "-" })
        {
            Require(!PanelCladdingAutoAttributeService.TryParsePsf(text, out _), $"PSF '{text}' is not available.");
        }
        foreach (var userText in new[]
                 {
                     new Dictionary<string, string>(),
                     new Dictionary<string, string> { ["CW_6.01_PSF"] = " " },
                     new Dictionary<string, string> { ["CW_6.01_PSF"] = "TBD" }
                 })
        {
            Require(PanelCladdingAutoAttributeService.CreateValues(90, 305, 190.625, userText)["CW_6.02_UNIT_WEIGHT"] == " ",
                "Without a numeric CW_6.01 the weight is the persisted blank.");
        }
        Console.WriteLine("[OK] PSF is the leading number of CW_6.01; otherwise the weight is blank");
    }

    private static void VerifyChanges()
    {
        var desired = PanelCladdingAutoAttributeService.CreateValues(
            90, 305, 190.625, new Dictionary<string, string> { ["CW_6.01_PSF"] = "85" });
        var current = new Dictionary<string, string>(desired) { ["CW_6.01_PSF"] = "85", ["AUTO_panel"] = "1" };
        Require(PanelCladdingAutoAttributeService.Changes(desired, current).Count == 0, "An up-to-date panel is not modified.");

        var stale = new Dictionary<string, string>(current)
        {
            ["CW_2.01_UNIT_WIDTH"] = "89.00000", ["CW_6.02_UNIT_WEIGHT"] = " "
        };
        stale.Remove("CW_2.07_UNIT_AREA");
        var changes = PanelCladdingAutoAttributeService.Changes(desired, stale);
        Require(changes.Count == 3 && changes["CW_2.01_UNIT_WIDTH"] == "90.00000" &&
                changes["CW_2.07_UNIT_AREA"] == "190.625 sqf" && changes["CW_6.02_UNIT_WEIGHT"] == "16203.125",
            "Only stale or missing values change.");

        var variant = new Dictionary<string, string>(current);
        variant.Remove("CW_2.02_UNIT_HEIGHT");
        variant["cw_2.02_unit_height"] = "305.00000";
        Require(PanelCladdingAutoAttributeService.Changes(desired, variant).Keys.SequenceEqual(new[] { "CW_2.02_UNIT_HEIGHT" }),
            "A case variant is rewritten under the canonical key.");

        var blank = PanelCladdingAutoAttributeService.CreateValues(90, 305, 190.625, new Dictionary<string, string>());
        var noWeight = new Dictionary<string, string>(blank);
        noWeight.Remove("CW_6.02_UNIT_WEIGHT");
        Require(PanelCladdingAutoAttributeService.Changes(blank, noWeight).Count == 0 &&
                PanelCladdingAutoAttributeService.Changes(blank, new Dictionary<string, string>(blank) { ["CW_6.02_UNIT_WEIGHT"] = "" }).Count == 0,
            "A missing or blank weight already satisfies a blank weight.");
        Require(PanelCladdingAutoAttributeService.Changes(blank, new Dictionary<string, string>(blank) { ["CW_6.02_UNIT_WEIGHT"] = "16203.125" })
                    ["CW_6.02_UNIT_WEIGHT"] == " ",
            "Removing the PSF blanks a stale weight.");
        Console.WriteLine("[OK] only stale values are written; case variants collapse; blank weight is not re-written");
    }

    private static void VerifyInputKeys()
    {
        Require(new[] { "CW_6.01_PSF", "cw_6.02_unit_weight", "CW_2.00_UNIT_DIMENSION", "CW_2.07_UNIT_AREA" }
                    .All(PanelCladdingAutoAttributeService.IsInputKey) &&
                !new[] { "AUTO_panel", "CW_1.01_PID", "CW_2.03_OFFSET_H0" }.Any(PanelCladdingAutoAttributeService.IsInputKey),
            "PSF and the refreshed keys re-queue a panel; other keys do not.");
        Console.WriteLine("[OK] attribute edits re-queue a panel only for CW_6.01 and the refreshed keys");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
