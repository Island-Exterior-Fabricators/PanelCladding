using System.Diagnostics;
using System.Text;

namespace RepoCheck;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
            IReadOnlyList<Finding> findings = Check(root, ListFiles(root));
            foreach (Finding finding in findings) Console.WriteLine(finding);
            if (findings.Count == 0)
            {
                Console.WriteLine("RepoCheck: no comments, one sentence per line, no timed waits in tests.");
                return 0;
            }
            Console.WriteLine();
            foreach (IGrouping<string, Finding> group in findings.GroupBy(finding => finding.Rule).OrderBy(group => group.Key, StringComparer.Ordinal))
                Console.WriteLine($"RepoCheck: {group.Count()} x {group.Key}");
            Console.WriteLine("The enforced rules are listed in docs/design.md#enforced-rules.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("RepoCheck failed: " + ex.Message);
            return 2;
        }
    }

    public static IReadOnlyList<Finding> Check(string root, IEnumerable<string> paths)
    {
        var findings = new List<Finding>();
        var powerShell = new List<(string Path, string FullPath)>();
        foreach (string path in paths.Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal))
        {
            string full = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full)) continue;
            string extension = Path.GetExtension(path);
            FileKind kind = FileKinds.Classify(path, extension.Length > 0 ? string.Empty : ReadText(full));
            if (kind == FileKind.Binary) continue;
            string text = ReadText(full);
            if (!Exclusions.Covers(Exclusions.CommentRulePaths, path))
            {
                if (kind == FileKind.PowerShell) powerShell.Add((path, full));
                else findings.AddRange(CommentRules.Find(path, text, kind));
            }
            if (FileKinds.IsProse(kind) && !Exclusions.Covers(Exclusions.ProseRulePaths, path))
                findings.AddRange(ProseRules.Find(path, text, kind));
            if (TimingRules.Applies(path, kind))
                findings.AddRange(TimingRules.Find(path, text));
        }
        findings.AddRange(CommentRules.PowerShell(powerShell));
        return findings
            .OrderBy(finding => finding.Path, StringComparer.Ordinal)
            .ThenBy(finding => finding.Line)
            .ToList();
    }

    public static IReadOnlyList<string> ListFiles(string root)
    {
        var info = new ProcessStartInfo("git")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (string argument in new[] { "-C", root, "ls-files", "-z", "--cached" })
            info.ArgumentList.Add(argument);
        using Process process = Process.Start(info) ?? throw new InvalidOperationException("git could not start.");
        Task<string> error = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("git ls-files failed: " + error.Result.Trim());
        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string ReadText(string path) => File.ReadAllText(path, Encoding.UTF8);
}
