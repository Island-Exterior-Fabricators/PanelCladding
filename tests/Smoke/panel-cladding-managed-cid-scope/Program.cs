using PanelCladdingEditor.Application.Services.PanelCladding;

namespace PanelCladdingManagedCidScopeSmoke;

internal static class Program
{
    private static void Main()
    {
        VerifyManagedLayerPredicates();
        VerifyLiveBakeDiscoveryContract();
        VerifyLiveSyncDiscoveryContract();
    }

    private static void VerifyManagedLayerPredicates()
    {
        Require(
            PanelCladdingSpawnPlanningService.IsManagedSurfaceLayerPath("Reference::Surfaces - STEP"),
            "The canonical STEP root must be managed.");
        Require(
            PanelCladdingSpawnPlanningService.IsManagedSurfaceLayerPath(
                "Reference::Surfaces - STEP::Surfaces - GLS::GLS-001"),
            "A canonical STEP descendant must be managed.");
        Require(
            !PanelCladdingSpawnPlanningService.IsManagedSurfaceLayerPath(
                "04_STEP Surfaces::Surfaces-Glass::GL01"),
            "The retired STEP tree must no longer participate in live sync or CID conflicts.");
        Require(
            !PanelCladdingSpawnPlanningService.IsManagedSurfaceLayerPath(
                "Reference::Surfaces - STEP Detail::GLS-001"),
            "A similar surface-layer prefix must not cross the segment boundary.");
        Require(
            !PanelCladdingSpawnPlanningService.IsManagedSurfaceLayerPath(
                "ABC-IEF-Wireframe::Surfaces - GLS::GLS-001"),
            "Wireframe material surfaces must not be managed surface output.");

        Require(
            PanelCladdingSpawnPlanningService.IsManagedExtrusionLayerPath("ABC-IEF-Wireframe::Curves - PNL"),
            "The canonical extrusion root must be managed.");
        Require(
            PanelCladdingSpawnPlanningService.IsManagedExtrusionLayerPath(
                "ABC-IEF-Wireframe::Curves - PNL::Curves - WT_04"),
            "A canonical extrusion descendant must be managed.");
        Require(
            !PanelCladdingSpawnPlanningService.IsManagedExtrusionLayerPath(
                "ABC-IEF-Wireframe::Curves - PNL Detail::Curves - WT_04"),
            "A similar extrusion-layer prefix must not cross the segment boundary.");
        Require(
            !PanelCladdingSpawnPlanningService.IsManagedExtrusionLayerPath(
                "02_CW Extrusions::Curves-PNL::WT-04"),
            "The retired extrusion tree must not be managed extrusion output.");
        Require(
            !PanelCladdingSpawnPlanningService.IsManagedExtrusionLayerPath(
                "ABC-IEF-Wireframe::Surfaces - PNL::Surfaces - WT_04"),
            "Source panel layers must not be managed extrusion output.");
        Console.WriteLine("[OK] managed layer predicates accept only canonical roots and descendants");
    }

    // The spawn commands and their CID-conflict check were removed in 1.3.0; PCbake discovers
    // existing dependencies with the same geometry-class and managed-root limits.
    private static void VerifyLiveBakeDiscoveryContract()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "PanelCladdingEditor",
            "Infrastructure",
            "Rhino",
            "Live",
            "PanelCladding",
            "LivePanelCladdingUpdateService.cs"));
        Require(
            source.Contains("Brep when PanelCladdingSpawnPlanningService.IsManagedSurfaceLayerPath(layerPath)", StringComparison.Ordinal),
            "Existing surface dependencies are not limited by geometry class and managed STEP layer.");
        Require(
            source.Contains("Curve when PanelCladdingSpawnPlanningService.IsManagedExtrusionLayerPath(layerPath,", StringComparison.Ordinal),
            "Existing curve dependencies are not limited by geometry class and managed extrusion layer.");
        Console.WriteLine("[OK] bake dependency discovery is filtered by geometry class and managed root");
    }

    private static void VerifyLiveSyncDiscoveryContract()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "PanelCladdingEditor",
            "Infrastructure",
            "Rhino",
            "Live",
            "PanelCladding",
            "LivePanelCladdingSurfaceSyncRepository.cs"));
        Require(
            source.Contains("IsManagedSurfaceLayerPath(layerPath)", StringComparison.Ordinal),
            "Live surface sync does not use the canonical STEP managed-root predicate.");
        Require(
            source.Contains("IsManagedExtrusionLayerPath(layerPath,", StringComparison.Ordinal),
            "Live curve sync does not use the canonical extrusion managed-root predicate.");
        Require(
            !source.Contains("IsUnderMaterialSurfaceRoot", StringComparison.Ordinal),
            "Legacy material-root discovery remains in the live sync repository.");
        Console.WriteLine("[OK] sync discovery is restricted to Reference::Surfaces - STEP and <PROJECT>-IEF-Wireframe::Curves - PNL");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new InvalidOperationException("Repository root containing AGENTS.md was not found.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
