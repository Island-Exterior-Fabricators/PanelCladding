using System.Reflection;
using System.Text.Json;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Rhino;
using Rhino.Runtime.InProcess;

namespace PCUpdateCommandUndoSmoke;

internal static class Program
{
    private const string NativeSwitch = "--rhino-undo";

    private static void Main(string[] args)
    {
        VerifySourceContract();
        VerifyPackageContract();

        if (!args.Contains(NativeSwitch, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                "[SKIP] native ambient Undo verification requires an initialized Rhino runtime; " +
                $"pass {NativeSwitch} in a supported Rhino test host");
            return;
        }

        string[] coreArguments = Environment.GetCommandLineArgs()
            .Where(argument => !argument.Equals(NativeSwitch, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        using var core = new RhinoCore(coreArguments, WindowStyle.NoWindow);
        VerifyAmbientUndoRecordIsReused();
    }

    private static void VerifySourceContract()
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

        string[] required =
        {
            "uint undoRecord = document.CurrentUndoRecordSerialNumber;",
            "bool ownsUndoRecord = undoRecord == 0U;",
            "if (ownsUndoRecord)",
            "undoRecord = document.BeginUndoRecord(\"Update Panel Cladding Dependencies\");"
        };
        foreach (string token in required)
        {
            Require(source.Contains(token, StringComparison.Ordinal),
                $"PCbake ambient Undo contract is missing: {token}");
        }

        int beginIndex = source.IndexOf(
            "undoRecord = document.BeginUndoRecord(\"Update Panel Cladding Dependencies\");",
            StringComparison.Ordinal);
        int ownershipGuardIndex = source.LastIndexOf(
            "if (ownsUndoRecord)",
            beginIndex,
            StringComparison.Ordinal);
        Require(ownershipGuardIndex >= 0 && ownershipGuardIndex < beginIndex,
            "PCbake must guard BeginUndoRecord with service ownership.");

        int finallyIndex = source.IndexOf("finally", beginIndex, StringComparison.Ordinal);
        int endIndex = source.IndexOf("document.EndUndoRecord(undoRecord);", finallyIndex, StringComparison.Ordinal);
        int rollbackIndex = source.IndexOf("!document.Undo()", endIndex, StringComparison.Ordinal);
        int finalOwnershipGuardIndex = source.LastIndexOf(
            "if (ownsUndoRecord)",
            endIndex,
            StringComparison.Ordinal);
        Require(finalOwnershipGuardIndex > finallyIndex && finalOwnershipGuardIndex < endIndex,
            "PCbake must not end an ambient Rhino command Undo record.");
        Require(rollbackIndex > endIndex,
            "Service-owned failure rollback must remain after EndUndoRecord.");
        Console.WriteLine("[OK] PCbake opens, closes, and rolls back only service-owned Undo records");
    }

    private static void VerifyPackageContract()
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Packaging",
            "PanelCladdingEditor",
            "package-manifest.json")));
        Require(Version.TryParse(manifest.RootElement.GetProperty("version").GetString(), out Version? version) &&
                version >= new Version(1, 0, 73),
            "PCbake Undo support requires package 1.0.73 or later.");
        Console.WriteLine("[OK] package supports PCbake Undo handling");
    }

    private static void VerifyAmbientUndoRecordIsReused()
    {
        using RhinoDoc document = RhinoDoc.CreateHeadless(null);
        uint ambientUndoRecord = document.BeginUndoRecord("PCbake ambient command simulation");
        Require(ambientUndoRecord != 0U, "The Rhino test host did not open an ambient Undo record.");

        try
        {
            Type serviceType = typeof(PanelCladdingDependencyReconciliationService).Assembly.GetType(
                "PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding.LivePanelCladdingUpdateService") ??
                throw new InvalidOperationException("LivePanelCladdingUpdateService is missing.");
            MethodInfo apply = serviceType.GetMethod(
                "Apply",
                BindingFlags.Instance | BindingFlags.NonPublic) ??
                throw new InvalidOperationException("LivePanelCladdingUpdateService.Apply is missing.");
            var keys = new PanelCladdingKeyService();
            object service = Activator.CreateInstance(serviceType,
                [null, keys, new PanelCladdingSpawnPlanningService(keys), new PanelCladdingDependencyReconciliationService()])!;

            object response = apply.Invoke(
                service,
                [document, "unused-for-empty-batch", Array.Empty<Guid>(), PanelCladdingBakeScope.All]) ??
                throw new InvalidOperationException("PCbake Apply returned null.");
            bool success = (bool?)response.GetType().GetProperty("Success")?.GetValue(response) ?? false;
            string message = (string?)response.GetType().GetProperty("Message")?.GetValue(response) ?? string.Empty;

            Require(success, $"PCbake rejected Rhino's ambient Undo record: {message}");
            Require(document.CurrentUndoRecordSerialNumber == ambientUndoRecord,
                "PCbake replaced or closed Rhino's ambient Undo record.");
            Console.WriteLine(
                $"[OK] native Rhino ambient Undo record {ambientUndoRecord} was reused and left open");
        }
        finally
        {
            document.EndUndoRecord(ambientUndoRecord);
        }
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
        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
