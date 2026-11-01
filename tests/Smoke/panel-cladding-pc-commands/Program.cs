using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using PanelCladdingEditor.Application.Services.PanelCladding;
using Rhino.Commands;

namespace PanelCladdingPcCommandsSmoke;

internal static class Program
{
    private static readonly IReadOnlyDictionary<string, string> ExpectedCommands =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PanelCladdingEditorCommand"] = "PCeditor",
            ["PanelCladdingCreateCommand"] = "PCcreate",
            ["PanelCladdingClearCommand"] = "PCclear",
            ["PanelCladdingCurveTemplateCommand"] = "PCcrvTemplate",
            ["PanelCladdingMatchCommand"] = "PCmatchSrf",
            ["PanelCladdingMatchCurveCommand"] = "PCmatchCrv",
            ["PanelCladdingSyncCrvCommand"] = "PCsyncCrv",
            ["PanelCladdingSyncSrfCommand"] = "PCsyncSrf",
            ["PanelCladdingBakeCommand"] = "PCbake",
            ["PanelCladdingPidCommand"] = "PCpid",
            ["PanelCladdingTemplatePnlCommand"] = "PCtemplatePNL"
        };

    private static int Main()
    {
        try
        {
            string repositoryRoot = FindRepositoryRoot();
            VerifyAssemblyCommandTypes();
            VerifyRegisteredNames(repositoryRoot);
            VerifyPackageMetadata(repositoryRoot);
            VerifyNoFormerSpellings(repositoryRoot);

            Console.WriteLine("[OK] All eleven user-facing PanelCladdingEditor Rhino commands use the PC prefix.");
            Console.WriteLine("[OK] Command names follow the PC + camel case rule; source and installer use no former spelling.");
            Console.WriteLine("[OK] No legacy PanelCladding-prefixed command alias remains registered.");
            Console.WriteLine("[OK] Command GUIDs remain explicit and unique, and package metadata names PCeditor only.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Panel cladding PC command smoke failed: {ex}");
            return 1;
        }
    }

    private static void VerifyAssemblyCommandTypes()
    {
        Assembly assembly = typeof(PanelCladdingKeyService).Assembly;
        Type[] commands = assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(Command).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();

        Require(commands.Select(type => type.Name).OrderBy(name => name, StringComparer.Ordinal)
                .SequenceEqual(ExpectedCommands.Keys.OrderBy(name => name, StringComparer.Ordinal)),
            $"Unexpected Rhino command type set: {string.Join(", ", commands.Select(type => type.Name))}");
        Require(commands.All(type => type.GUID != Guid.Empty),
            "Every Rhino command must retain an explicit non-empty GUID.");
        Require(commands.Select(type => type.GUID).Distinct().Count() == commands.Length,
            "Every Rhino command must retain a unique GUID.");
    }

    private static void VerifyRegisteredNames(string repositoryRoot)
    {
        string commandRoot = Path.Combine(repositoryRoot, "src", "PanelCladdingEditor", "UI");
        var actualNames = new List<string>();
        foreach ((string commandType, string expectedName) in ExpectedCommands)
        {
            string path = Path.Combine(commandRoot, $"{commandType}.cs");
            string source = File.ReadAllText(path);
            Match match = Regex.Match(
                source,
                "public\\s+override\\s+string\\s+EnglishName\\s*=>\\s*\\\"([^\\\"]+)\\\"\\s*;",
                RegexOptions.CultureInvariant);
            Require(match.Success, $"{commandType} does not declare a literal EnglishName.");

            string actualName = match.Groups[1].Value;
            actualNames.Add(actualName);
            Require(actualName == expectedName,
                $"{commandType} registers '{actualName}' instead of '{expectedName}'.");
            Require(actualName.StartsWith("PC", StringComparison.Ordinal),
                $"{actualName} does not use the PC prefix.");
            // Since 1.3.0: PC, the first word lowercase, later words capitalized; PNL stays an acronym.
            Require(Regex.IsMatch(actualName, "^PC[a-z]+(?:[A-Z][a-z]+|PNL)*$", RegexOptions.CultureInvariant),
                $"{actualName} does not follow the PC + camel case naming rule.");
            Require(!actualName.StartsWith("PanelCladding", StringComparison.OrdinalIgnoreCase),
                $"Legacy command prefix remains registered as {actualName}.");

            foreach (Match writeCall in Regex.Matches(
                         source,
                         "RhinoApp\\.WriteLine\\((?<body>.*?)\\);",
                         RegexOptions.CultureInvariant | RegexOptions.Singleline))
            {
                string body = writeCall.Groups["body"].Value;
                bool containsLegacyCommandPrefix =
                    body.Contains("PanelCladding", StringComparison.OrdinalIgnoreCase);
                bool isProductIdentityMessage = body.Contains(
                    "PanelCladdingEditor is a standalone Rhino plug-in",
                    StringComparison.Ordinal);
                Require(!containsLegacyCommandPrefix || isProductIdentityMessage,
                    $"{commandType} retains a legacy command name in Rhino command-line output.");
            }
        }

        Require(actualNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() == ExpectedCommands.Count,
            "Registered Rhino command names must be unique without regard to case.");
    }

    private static void VerifyPackageMetadata(string repositoryRoot)
    {
        string manifestPath = Path.Combine(
            repositoryRoot, "Packaging", "PanelCladdingEditor", "package-manifest.json");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement root = manifest.RootElement;
        Require(root.GetProperty("name").GetString() == "PanelCladdingEditor",
            "The product identity must remain PanelCladdingEditor.");
        Require(root.GetProperty("command").GetString() == "PCeditor",
            "Package metadata must name PCeditor as the primary command.");
        Require(!root.TryGetProperty("smokeCommand", out _),
            "Package metadata must not expose a production smoke command.");
    }

    // Spellings before 1.3.0 still run in Rhino (names are case-insensitive) but must not be
    // written in source messages or the installer. The package README names them on purpose.
    // PCupdate, PCspawnSrf and PCspawnCrv no longer exist (PCbake replaces them) in any case;
    // comment lines may still mention them.
    private static void VerifyNoFormerSpellings(string repositoryRoot)
    {
        var formerSpelling = new Regex(
            "(?<![A-Za-z0-9])PC(?:Clear|Create|CrvTemplate|Editor|MatchCrv|MatchSrf|SyncCrv|SyncSrf)\\b",
            RegexOptions.CultureInvariant);
        var removedCommand = new Regex(
            "(?<![A-Za-z0-9])PC(?:update|spawnSrf|spawnCrv)\\b",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        string sourceRoot = Path.Combine(repositoryRoot, "src", "PanelCladdingEditor");
        string packageRoot = Path.Combine(repositoryRoot, "Packaging", "PanelCladdingEditor");
        IEnumerable<string> files = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .Concat(new[] { "Install-PanelCladdingEditor.ps1", "Install-Package.ps1", "QUICKSTART.txt", "package-manifest.json" }
                .Select(name => Path.Combine(packageRoot, name)));
        foreach (string path in files)
        {
            string text = File.ReadAllText(path);
            Match match = formerSpelling.Match(text);
            Require(!match.Success,
                $"{Path.GetRelativePath(repositoryRoot, path)} still writes the former spelling {match.Value}.");
            string code = string.Join('\n', text.Split('\n')
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
            Match removed = removedCommand.Match(code);
            Require(!removed.Success,
                $"{Path.GetRelativePath(repositoryRoot, path)} still names the removed command {removed.Value}.");
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src", "PanelCladdingEditor")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the PanelCladding repository root.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
