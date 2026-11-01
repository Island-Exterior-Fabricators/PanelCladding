using System.Reflection;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Rhino.Commands;

namespace PanelCladdingSplitSpawnSyncSmoke;

internal static class Program
{
    private static void Main()
    {
        Assembly assembly = typeof(PanelCladdingKeyService).Assembly;
        // Since 1.3.0 PCbake bakes surfaces and/or curves by its Scope option; the scoped spawn
        // commands are gone. Sync stays split by object family.
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PanelCladdingBakeCommand"] = "PCbake",
            ["PanelCladdingSyncSrfCommand"] = "PCsyncSrf",
            ["PanelCladdingSyncCrvCommand"] = "PCsyncCrv"
        };
        foreach ((string typeName, string commandName) in expected)
        {
            Type type = assembly.GetType($"PanelCladdingEditor.UI.{typeName}") ??
                throw new InvalidOperationException($"Missing command type {typeName}.");
            var command = (Command)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
            Require(command.EnglishName == commandName, $"{typeName} registered {command.EnglishName}.");
        }
        Require(new[]
                {
                    "PanelCladdingSpawnCommand", "PanelCladdingSyncCommand",
                    "PanelCladdingSpawnSrfCommand", "PanelCladdingSpawnCrvCommand", "PanelCladdingUpdateCommand"
                }.All(name => assembly.GetType($"PanelCladdingEditor.UI.{name}") is null),
            "A removed spawn, update or combined sync command remains compiled.");
        Require(Enum.GetValues<PanelCladdingBakeScope>().SequenceEqual(
                [PanelCladdingBakeScope.Surfaces, PanelCladdingBakeScope.Curves, PanelCladdingBakeScope.All]),
            "PCbake scope order changed; Surfaces must stay the first (default) option.");

        var keys = new PanelCladdingKeyService();
        PanelCladdingKeySet blankGrid = Required(keys.CreateKeySet(
            [30d], [40d], new Dictionary<string, string>(), 100d, 80d, 0.001d));
        IReadOnlyList<PanelCladdingExtrusionCurvePlan> curves = Required(
            new PanelCladdingExtrusionPlanningService().CreatePlan(
                "PID-SAMPLE", "CID-SAMPLE", 100d, 80d, blankGrid,
                "ABC-IEF-Wireframe::Surfaces - PNL::Surfaces - WT-01"));
        Require(curves.Count > 4 && curves.All(curve =>
                curve.LayerPath == "ABC-IEF-Wireframe::Curves - PNL::Curves - WT-01"),
            "Curve planning did not work independently of cladding assignments.");

        Require(Enum.GetValues<PanelCladdingObjectScope>().SequenceEqual(
                [PanelCladdingObjectScope.Surfaces, PanelCladdingObjectScope.Curves]),
            "Object-family scope contract changed unexpectedly.");
        Console.WriteLine("[OK] removed spawn/update commands are absent; PCbake and the two scoped sync commands are registered.");
        Console.WriteLine("[OK] curve spawn planning succeeds with blank cladding assignments.");
        Console.WriteLine("[OK] surface/curve scope is explicit in the shared application contract.");
    }

    private static T Required<T>(PanelCladdingEditor.Contracts.Responses.OperationResponse<T> response)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException(response.Message);
        }
        return response.Data;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
