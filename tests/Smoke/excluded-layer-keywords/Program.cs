using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;

namespace ExcludedLayerKeywordsSmoke;

// Since 1.3.0 layers named "Not in Scope" or "By Others" are not tracked by any panel or curve scope;
// after 1.3.0 the auto attributes still refresh panels on them.
internal static class Program
{
    private const string PanelBranch = "BKT-IEF-Wireframe::Surfaces - PNL";
    private const string CurveBranch = "BKT-IEF-Wireframe::Curves - PNL";

    private static int Main()
    {
        try
        {
            VerifyKeywordMatching();
            VerifyScopes();
            VerifyCurveRouting();
            Console.WriteLine("PASS: excluded layer keywords regression");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Excluded layer keywords smoke failed: {ex}");
            return 1;
        }
    }

    private static void VerifyKeywordMatching()
    {
        foreach (string path in new[]
                 {
                     PanelBranch + "::Surfaces - Not in Scope",
                     PanelBranch + "::Surfaces - NOT_IN_SCOPE",
                     PanelBranch + "::Surfaces - Not-In-Scope::WT_01",
                     PanelBranch + "::Surfaces - By Others",
                     PanelBranch + "::Tower A::By-Others",
                     CurveBranch + "::Curves - by others"
                 })
        {
            Require(PanelCladdingExtrusionPlanningService.IsExcludedLayerPath(path), "Must be excluded: " + path);
        }
        foreach (string path in new[]
                 {
                     PanelBranch, PanelBranch + "::Surfaces - WT_01", PanelBranch + "::Surfaces - In Scope",
                     PanelBranch + "::Surfaces - Others", CurveBranch + "::Curves - WT_01", ""
                 })
        {
            Require(!PanelCladdingExtrusionPlanningService.IsExcludedLayerPath(path), "Must stay tracked: " + path);
        }
        Console.WriteLine("[OK] keywords match in any segment, ignoring case, spaces, '-' and '_'");
    }

    private static void VerifyScopes()
    {
        Require(!PanelCladdingPidPlanningService.IsPanelLayerPath(PanelBranch + "::Surfaces - Not in Scope") &&
                !PanelCladdingExtrusionPlanningService.IsPanelLayerPath(PanelBranch + "::Surfaces - By Others::WT_01", "BKT") &&
                PanelCladdingPidPlanningService.IsPanelLayerPath(PanelBranch + "::Surfaces - WT_01"),
            "Panel scope (PCpid, naming sync, report) must skip excluded layers only.");
        // Auto attributes keep excluded panels' sizes and UD_PLANE current (after 1.3.0).
        Require(PanelCladdingExtrusionPlanningService.IsPanelBranchPath(PanelBranch + "::Surfaces - Not in Scope", "BKT") &&
                PanelCladdingExtrusionPlanningService.IsPanelBranchPath(PanelBranch + "::Surfaces - By Others::WT_01", "BKT") &&
                PanelCladdingExtrusionPlanningService.IsPanelBranchPath(PanelBranch + "::Surfaces - WT_01", "BKT") &&
                PanelCladdingExtrusionPlanningService.IsPanelBranchPath(PanelBranch, "BKT") &&
                !PanelCladdingExtrusionPlanningService.IsPanelBranchPath(CurveBranch + "::Curves - WT_01", "BKT") &&
                !PanelCladdingExtrusionPlanningService.IsPanelBranchPath("MET-IEF-Wireframe::Surfaces - PNL::Surfaces - WT_01", "BKT") &&
                !PanelCladdingExtrusionPlanningService.IsPanelBranchPath("Reference::Surfaces - Not in Scope", "BKT"),
            "The panel branch includes its excluded sublayers and nothing outside the project's Surfaces - PNL.");
        string autoAttributes = File.ReadAllText(Path.Combine(RegressionPaths.RepositoryRoot,
            "src", "PanelCladdingEditor", "Infrastructure", "Rhino", "Live", "PanelCladding", "LivePanelAutoAttributes.cs"));
        Require(autoAttributes.Contains("PanelCladdingExtrusionPlanningService.IsPanelBranchPath(layerPath, project)", StringComparison.Ordinal) &&
                !autoAttributes.Contains("IsPanelLayerPath", StringComparison.Ordinal),
            "The auto attributes must refresh the whole panel branch, excluded sublayers included.");
        Require(!PanelCladdingSpawnPlanningService.IsManagedExtrusionLayerPath(CurveBranch + "::Curves - Not in Scope", "BKT") &&
                PanelCladdingSpawnPlanningService.IsManagedExtrusionLayerPath(CurveBranch + "::Curves - WT_01", "BKT"),
            "Managed curve scope must skip excluded layers only.");
        Console.WriteLine("[OK] panel and managed-curve scopes skip excluded layers; auto attributes cover the whole panel branch");
    }

    private static void VerifyCurveRouting()
    {
        OperationResponse<string> excluded =
            PanelCladdingExtrusionPlanningService.ResolveCurveLayerPath(PanelBranch + "::Surfaces - Not in Scope");
        Require(!excluded.Success && excluded.Message.StartsWith("PANEL_CLADDING_PANEL_LAYER_EXCLUDED", StringComparison.Ordinal),
            "An excluded panel must not get a curve layer.");
        OperationResponse<string> tracked =
            PanelCladdingExtrusionPlanningService.ResolveCurveLayerPath(PanelBranch + "::Surfaces - WT_01");
        Require(tracked.Success && tracked.Data == CurveBranch + "::Curves - WT_01", "Tracked routing changed.");
        Console.WriteLine("[OK] PCbake does not route curves for excluded panels");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
