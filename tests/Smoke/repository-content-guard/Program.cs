using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RepositoryContentGuardSmoke;

// The repository is public. Real project models, their exports and company file-server locations
// must stay out of it: test fixtures use fictional project codes (ABC, XYZ, QRS, JKL) and raw
// results of live runs against project models are not committed (AGENTS.md). This smoke checks
// every tracked file of the checkout the regression runs in.
internal static class Program
{
    // Model, drawing, spreadsheet and document formats that only come from real project work.
    private static readonly string[] ForbiddenExtensions =
        [".3dm", ".3dmbak", ".rhl", ".dwg", ".dxf", ".rvt", ".xls", ".xlsx", ".xlsm", ".pdf"];

    // Company file-server layout and project folder names ("P12345 Some Project").
    private static readonly (Regex Pattern, string Reason)[] ForbiddenText =
    [
        (new Regex(@"0\d Project Folders", RegexOptions.IgnoreCase), "company project-folder root"),
        (new Regex(@"0\d-Design-Eng|0\d-BIM", RegexOptions.IgnoreCase), "company project-folder layout"),
        (new Regex(@"\\EgnyteDrive\b", RegexOptions.IgnoreCase), "company file-server location"),
        (new Regex(@"(?<![A-Za-z0-9])P\d{5} [A-Z][a-z]"), "project number followed by a project name"),
    ];

    private static int Main()
    {
        try
        {
            string[] files = TrackedFiles();
            Require(files.Length > 100 && files.Contains("tests/Run-Regression.ps1"),
                "The smoke must run from the repository root (Run-Regression.ps1 sets it).");
            var problems = new List<string>();
            foreach (string file in files)
            {
                if (ForbiddenExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                    problems.Add($"{file}: project data file type");
                if (file.StartsWith("tests/Smoke/repository-content-guard/", StringComparison.Ordinal) || !File.Exists(file))
                    continue;
                byte[] bytes = File.ReadAllBytes(file);
                if (bytes.Contains((byte)0)) continue;
                string text = System.Text.Encoding.UTF8.GetString(bytes);
                foreach ((Regex pattern, string reason) in ForbiddenText)
                {
                    Match match = pattern.Match(text);
                    if (match.Success) problems.Add($"{file}: {reason} ('{match.Value}')");
                }
            }
            Require(problems.Count == 0, "Client project data in tracked files:\n  " + string.Join("\n  ", problems));
            Console.WriteLine($"[OK] {files.Length} tracked files: no project data file types or company project-folder paths");
            VerifyPatterns();
            Console.WriteLine("PASS: repository content guard");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Repository content guard failed: {ex}");
            return 1;
        }
    }

    // The patterns catch the shapes they are meant for and leave fixture text alone.
    private static void VerifyPatterns()
    {
        string[] caught =
        [
            @"V:\01 Project Folders\P12345 Some Tower ABC\Wireframe\ABC - Wireframe.3dm",
            @"\\EgnyteDrive\Projects\x.3dm", "03-Design-Eng/03-BIM/05-Wireframe", "P54321 Harbor View",
        ];
        string[] allowed =
        [
            @"C:\Projects\ABC\Wireframe\ABC - Wireframe.3dm", "P00001-IEF-Wireframe", "PID-ABC-N1-01-07",
            "XYZ - Naming Convention", "a cloud drive (Egnyte) can hold a new file", "V: is an Egnyte drive",
        ];
        foreach (string sample in caught)
            Require(ForbiddenText.Any(rule => rule.Pattern.IsMatch(sample)), "Pattern misses: " + sample);
        foreach (string sample in allowed)
            Require(!ForbiddenText.Any(rule => rule.Pattern.IsMatch(sample)), "Pattern flags fixture text: " + sample);
        Console.WriteLine("[OK] guard patterns catch project-folder paths and leave fictional fixtures alone");
    }

    private static string[] TrackedFiles()
    {
        var info = new ProcessStartInfo("git", "ls-files -z")
        {
            RedirectStandardOutput = true, UseShellExecute = false, WorkingDirectory = Environment.CurrentDirectory,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
        };
        using Process git = Process.Start(info) ?? throw new InvalidOperationException("git could not start.");
        string output = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        Require(git.ExitCode == 0, "git ls-files failed.");
        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
