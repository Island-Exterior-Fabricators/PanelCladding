using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RepositoryContentGuardSmoke;

// The repository is public. The hooks in .githooks run .githooks/Test-RepositoryContent.ps1 on every
// commit and push so that client project data (real project names, codes, numbers, file-server paths,
// model exports, raw live-run output) never reaches it (AGENTS.md). This smoke checks that the rules
// catch what they are meant for, that the hooks are enabled, that the tracked files pass the guard,
// and that the hooks refuse and allow the right commits and pushes in a scratch repository.
internal static class Program
{
    private const string Guard = ".githooks/Test-RepositoryContent.ps1";
    private const string ListVariable = "PANELCLADDING_CONTENT_DENYLIST";
    private static readonly string[] Hooks = ["pre-commit", "commit-msg", "pre-push"];

    private static int Main()
    {
        try
        {
            string repo = RegressionPaths.RepositoryRoot;
            VerifyRules(repo);
            VerifyHooksEnabled(repo);
            VerifyTrackedFiles(repo);
            VerifyIgnoredOutput(repo);
            VerifyHookBehaviour(repo);
            Console.WriteLine("PASS: repository content guard");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Repository content guard failed: {ex}");
            return 1;
        }
    }

    // The shared rules catch the shapes they are meant for and leave fictional fixtures alone.
    private static void VerifyRules(string repo)
    {
        using JsonDocument rules = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, ".githooks", "content-rules.json")));
        Regex[] text = rules.RootElement.GetProperty("blockedText").EnumerateArray()
            .Select(rule => new Regex(rule.GetProperty("pattern").GetString()!,
                rule.GetProperty("ignoreCase").GetBoolean() ? RegexOptions.IgnoreCase : RegexOptions.None))
            .ToArray();
        Regex[] paths = rules.RootElement.GetProperty("blockedPaths").EnumerateArray()
            .Select(rule => new Regex(rule.GetProperty("pattern").GetString()!, RegexOptions.IgnoreCase))
            .ToArray();
        string[] extensions = rules.RootElement.GetProperty("blockedExtensions").EnumerateArray().Select(item => item.GetString()!).ToArray();

        string[] caughtText =
        [
            @"V:\01 Project Folders\P12345 Some Tower ABC\Wireframe\ABC - Wireframe.3dm",
            @"\\EgnyteDrive\Projects\x.3dm", "03-Design-Eng/03-BIM/05-Wireframe", "P54321 Harbor View",
        ];
        string[] allowedText =
        [
            @"C:\Projects\ABC\Wireframe\ABC - Wireframe.3dm", "P00001-IEF-Wireframe", "PID-ABC-N1-01-07",
            "XYZ - Naming Convention", "a cloud drive (Egnyte) can hold a new file", "V: is an Egnyte drive",
        ];
        string[] caughtPaths =
        [
            "Project_Test/261105_TEST_x/results/bake.json", "Project_Test/261105_TEST_x/results-abc/report.json",
            "Project_Test/261105_TEST_x/live/web.json", "Project_Archive/Project_Test/261031_TEST_x/live-abc/a.txt",
            "Project_Test/261105_TEST_x/report-window.png", "Project_Archive/Project_Test/261105_TEST_x/shot.JPG",
        ];
        string[] allowedPaths =
        [
            "Project_Test/261031_TEST_live-acceptance-tests/README.md", "Project_Test/261105_TEST_x/RESULTS.md",
            "tests/Live/PcLive.cs", "tests/Smoke/x/results.cs", "docs/diagram.png",
        ];
        foreach (string sample in caughtText)
            Require(text.Any(rule => rule.IsMatch(sample)), "Text rules miss: " + sample);
        foreach (string sample in allowedText)
            Require(!text.Any(rule => rule.IsMatch(sample)), "Text rules flag fixture text: " + sample);
        foreach (string sample in caughtPaths)
            Require(paths.Any(rule => rule.IsMatch(sample)), "Path rules miss: " + sample);
        foreach (string sample in allowedPaths)
            Require(!paths.Any(rule => rule.IsMatch(sample)), "Path rules flag: " + sample);
        Require(new[] { ".3dm", ".dwg", ".xlsx", ".pdf" }.All(extensions.Contains), "Project data file types are missing from the rules.");
        Console.WriteLine("[OK] rules catch project-folder paths, raw output folders and evidence images, and leave fixtures alone");
    }

    // Run-Regression.ps1 enables the hooks; each one hands its arguments to the guard script under sh.
    private static void VerifyHooksEnabled(string repo)
    {
        (int code, string output) = Run(repo, "git", null, "config", "--get", "core.hooksPath");
        Require(code == 0 && output.Trim() == ".githooks",
            $"core.hooksPath is '{output.Trim()}', not .githooks: run tests/Run-Regression.ps1 or git config core.hooksPath .githooks.");
        foreach (string hook in Hooks)
        {
            string text = File.ReadAllText(Path.Combine(repo, ".githooks", hook));
            Require(text.StartsWith("#!/bin/sh\n", StringComparison.Ordinal) && !text.Contains('\r') && text.Contains("Test-RepositoryContent.ps1"),
                $".githooks/{hook} must be an LF sh script that runs the guard.");
            (code, output) = Run(repo, "git", null, "check-attr", "eol", "--", ".githooks/" + hook);
            Require(output.Trim().EndsWith("eol: lf", StringComparison.Ordinal), $".githooks/{hook} must check out with LF (.gitattributes).");
        }
        Console.WriteLine("[OK] core.hooksPath = .githooks; pre-commit, commit-msg and pre-push are LF sh scripts");
    }

    // The tracked files pass the same guard the hooks use, with this machine's private list if it has one.
    private static void VerifyTrackedFiles(string repo)
    {
        (int code, string output) = RunGuard(repo, null, "-Mode", "Tracked");
        Require(code == 0, "Tracked files carry client project data:\n" + output);
        Console.WriteLine("[OK] " + output.Trim().Replace(Environment.NewLine, " | "));
    }

    private static void VerifyIgnoredOutput(string repo)
    {
        foreach (string path in new[] { "Project_Test/261105_TEST_x/results/bake.json", "Project_Test/261105_TEST_x/live-abc/web.json" })
            Require(Run(repo, "git", null, "check-ignore", "-q", "--no-index", path).Code == 0, ".gitignore does not ignore " + path);
        Require(Run(repo, "git", null, "check-ignore", "-q", "--no-index", "Project_Test/261105_TEST_x/README.md").Code == 1,
            ".gitignore ignores a TEST README.");
        Console.WriteLine("[OK] .gitignore keeps raw live-output folders under Project_Test out");
    }

    private static void VerifyHookBehaviour(string repo)
    {
        string root = Path.Combine(RegressionPaths.Output("repository-content-guard"), "scratch-" + Guid.NewGuid().ToString("N")[..8]);
        string work = Path.Combine(root, "work");
        string remote = Path.Combine(root, "remote.git");
        string list = Path.Combine(root, "client-list.txt");
        Directory.CreateDirectory(Path.Combine(work, ".githooks"));
        File.WriteAllText(list, "# fictional client for this smoke\nZorblax Tower\nre:(?-i)\\bQQX\\b\n");
        var withList = new Dictionary<string, string> { [ListVariable] = list };
        try
        {
            Git(work, withList, "init", "-q", "-b", "main");
            Git(work, withList, "config", "user.name", "Content Guard Smoke");
            Git(work, withList, "config", "user.email", "content-guard@example.invalid");
            Git(work, withList, "config", "core.autocrlf", "false");
            Git(work, withList, "config", "commit.gpgsign", "false");
            Git(work, withList, "config", "core.hooksPath", ".githooks");
            foreach (string file in Directory.GetFiles(Path.Combine(repo, ".githooks")))
                File.Copy(file, Path.Combine(work, ".githooks", Path.GetFileName(file)));
            File.Copy(Path.Combine(repo, ".gitattributes"), Path.Combine(work, ".gitattributes"));
            File.Copy(Path.Combine(repo, ".gitignore"), Path.Combine(work, ".gitignore"));
            Git(root, withList, "init", "-q", "--bare", remote);
            Git(work, withList, "remote", "add", "origin", remote);

            Commit(work, withList, "the hooks themselves", true, null);
            Require(Run(work, "git", withList, "push", "-q", "origin", "main").Code == 0, "A clean push was refused.");

            Commit(work, withList, "a fictional note", true, null, ("notes.md", "ABC - Wireframe.3dm opened at C:\\Projects\\ABC"));
            Commit(work, withList, "a workbook", false, "project data file type", ("ABC - Report.xlsx", "x"));
            Commit(work, withList, "a private list name in a file", false, "private client list entry 1", ("notes.md", "Panels for the zorblax tower"));
            Commit(work, withList, "a case-sensitive private entry", false, "private client list entry 2", ("ids.md", "PID-QQX-N1-01-01"));
            Commit(work, withList, "the same letters in lower case", true, null, ("ids.md", "qqx is not a code here"));
            Commit(work, withList, "a company project folder", false, "company project-folder root", ("paths.md", @"V:\01 Project Folders\P12345 Some Tower\a.3dm"));
            Commit(work, withList, "a private list name in a file name", false, "in the file path", ("Zorblax Tower notes.md", "x"));
            Commit(work, withList, "raw live output", false, "raw live-run output folder", ("Project_Test/261105_TEST_x/results/bake.json", "{}"));
            Commit(work, withList, "a new evidence image", false, "new image in test evidence", ("Project_Test/261105_TEST_x/shot.png", "\u0089PNG\0"));
            CommitMessage(work, withList, "a private list name in the message", false, "Record the Zorblax Tower run");

            // An image already on the remote may move into the archive.
            Write(work, "Project_Test/260101_TEST_old/view.png", "\u0089PNG\0old");
            Git(work, withList, "add", "-A");
            Git(work, withList, "commit", "-q", "--no-verify", "-m", "Older evidence image");
            Git(work, withList, "push", "-q", "--no-verify", "origin", "main");
            Directory.CreateDirectory(Path.Combine(work, "Project_Archive", "Project_Test"));
            Git(work, withList, "mv", "Project_Test/260101_TEST_old", "Project_Archive/Project_Test/260101_TEST_old");
            Require(Run(work, "git", withList, "commit", "-q", "-m", "Archive the older record").Code == 0, "Archiving an existing image was refused.");
            Require(Run(work, "git", withList, "push", "-q", "origin", "main").Code == 0, "Pushing an archive move was refused.");

            // A commit made with --no-verify is still refused at push time, for its files and its message.
            PushRefused(work, withList, "a bypassed commit's file", "private client list entry 1", ("leak.md", "zorblax tower"), "Bypassed commit");
            PushRefused(work, withList, "a bypassed commit's message", "message", ("fine.md", "fine"), "Notes on Zorblax Tower");
            Require(Run(work, "git", withList, "push", "-q", "origin", "main").Code == 0, "A push with nothing new was refused.");

            // Without a private list the structural rules still apply, and the guard says so.
            var withoutList = new Dictionary<string, string> { [ListVariable] = Path.Combine(root, "missing.txt") };
            (int code, string output) = Commit(work, withoutList, "a clean note without a private list", true, null, ("more.md", "fine"));
            Require(output.Contains("no private client list", StringComparison.Ordinal), "The guard did not report the missing private list.");
            Commit(work, withoutList, "a workbook without a private list", false, "project data file type", ("Other.xlsm", "x"));

            Console.WriteLine("[OK] hooks refuse workbooks, private list names (files, file names, messages, bypassed commits),");
            Console.WriteLine("     company folders, raw live output and new evidence images; they allow clean work and archive moves");
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static (int Code, string Output) Commit(string work, Dictionary<string, string> environment, string label, bool allowed,
        string? reason, params (string Path, string Text)[] files)
    {
        foreach ((string path, string text) in files) Write(work, path, text);
        Git(work, environment, "add", "-A", "-f");
        (int code, string output) = Run(work, "git", environment, "commit", "-q", "-m", "Test: " + label);
        CheckOutcome(work, environment, label, allowed, reason, code, output);
        return (code, output);
    }

    private static void CommitMessage(string work, Dictionary<string, string> environment, string label, bool allowed, string message)
    {
        Write(work, "message.md", label);
        Git(work, environment, "add", "-A");
        (int code, string output) = Run(work, "git", environment, "commit", "-q", "-m", message);
        CheckOutcome(work, environment, label, allowed, "commit message", code, output);
    }

    private static void PushRefused(string work, Dictionary<string, string> environment, string label, string reason, (string Path, string Text) file, string message)
    {
        Write(work, file.Path, file.Text);
        Git(work, environment, "add", "-A");
        Git(work, environment, "commit", "-q", "--no-verify", "-m", message);
        (int code, string output) = Run(work, "git", environment, "push", "-q", "origin", "main");
        Require(code != 0 && output.Contains(reason, StringComparison.Ordinal), $"Push of {label} was not refused:\n{output}");
        Require(!output.Contains("zorblax", StringComparison.OrdinalIgnoreCase), "The guard printed the matched text.");
        Git(work, environment, "reset", "-q", "--hard", "HEAD~1");
    }

    private static void CheckOutcome(string work, Dictionary<string, string> environment, string label, bool allowed, string? reason, int code, string output)
    {
        if (allowed)
        {
            Require(code == 0, $"Commit of {label} was refused:\n{output}");
            return;
        }
        Require(code != 0, $"Commit of {label} was not refused.");
        Require(reason is null || output.Contains(reason, StringComparison.Ordinal), $"Commit of {label} was refused for another reason:\n{output}");
        Require(!Regex.IsMatch(output, @"Panels for|PID-QQX|Record the Zorblax"), "The guard printed the matched text.");
        Git(work, environment, "reset", "-q", "--hard");
        Git(work, environment, "clean", "-q", "-f", "-d", "-x");
    }

    private static (int Code, string Output) RunGuard(string directory, Dictionary<string, string>? environment, params string[] arguments) =>
        Run(directory, "powershell.exe", environment,
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(directory, Guard), .. arguments]);

    private static void Git(string directory, Dictionary<string, string> environment, params string[] arguments)
    {
        (int code, string output) = Run(directory, "git", environment, arguments);
        Require(code == 0, $"git {string.Join(' ', arguments)} failed:\n{output}");
    }

    private static (int Code, string Output) Run(string directory, string file, Dictionary<string, string>? environment, params string[] arguments)
    {
        var info = new ProcessStartInfo(file)
        {
            WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (string argument in arguments) info.ArgumentList.Add(argument);
        foreach (string name in info.Environment.Keys.Where(key => key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToList())
            info.Environment.Remove(name);
        if (environment is not null)
            foreach ((string name, string value) in environment) info.Environment[name] = value;
        using Process process = Process.Start(info) ?? throw new InvalidOperationException(file + " could not start.");
        process.StandardInput.Close();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120_000))
        {
            process.Kill(true);
            throw new TimeoutException($"{file} {string.Join(' ', arguments)} did not finish.");
        }
        return (process.ExitCode, output.Result + error.Result);
    }

    private static void Write(string work, string path, string text)
    {
        string full = Path.Combine(work, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    private static void TryDelete(string path)
    {
        try
        {
            foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);   // git marks its objects read-only
            Directory.Delete(path, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
