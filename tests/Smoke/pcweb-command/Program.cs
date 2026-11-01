using System.Diagnostics;
using PanelCladdingEditor.Infrastructure.WireframeWeb;

namespace PcWebCommandSmoke;

// PCweb: finds the Wireframe Web launcher, checks the model name, and runs a fake launcher (in a
// folder with spaces) through every exit the launch contract defines. The command itself needs
// Rhino; its source is checked for the save question, the worker-thread launch and a launcher
// started without Rhino's handles.
internal static class Program
{
    private const string Address = "http://127.0.0.1:8795/?source=api&project=met&user=tester";
    private static readonly string Root = RegressionPaths.Output("pcweb-command");
    private static readonly string Log = Path.Combine(Root, "launcher-calls.txt");

    private static int Main()
    {
        try
        {
            VerifyFindLauncher();
            VerifyProjectCode();
            VerifyOpen();
            VerifyCommandSource();
            Console.WriteLine("PASS: PCweb command regression");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"PCweb smoke failed: {ex}");
            return 1;
        }
    }

    private static void VerifyFindLauncher()
    {
        string localAppData = Fresh("LocalAppData");
        string registered = Path.Combine(Fresh("Elsewhere"), WireframeWebLauncher.LauncherFileName);
        string fallback = Path.Combine(localAppData, "Programs", "Wireframe Web", WireframeWebLauncher.LauncherFileName);

        Require(WireframeWebLauncher.FindLauncher(registered, localAppData) is null, "No launcher file: null.");
        Directory.CreateDirectory(Path.GetDirectoryName(fallback)!);
        File.WriteAllText(fallback, "@exit /b 0");
        Require(WireframeWebLauncher.FindLauncher(registered, localAppData) == fallback, "Missing registered file: fallback.");
        Require(WireframeWebLauncher.FindLauncher(null, localAppData) == fallback, "No registry value: fallback.");
        Require(WireframeWebLauncher.FindLauncher("  ", localAppData) == fallback, "Blank registry value: fallback.");
        File.WriteAllText(registered, "@exit /b 0");
        Require(WireframeWebLauncher.FindLauncher(registered, localAppData) == registered, "Registered file wins.");
        Require(WireframeWebLauncher.FindLauncher($" {registered} ", localAppData) == registered, "Registered value is trimmed.");
        Require(WireframeWebLauncher.NotInstalledMessage.Contains(WireframeWebLauncher.DownloadPage, StringComparison.Ordinal) &&
                WireframeWebLauncher.NotInstalledMessage.Contains("Install Wireframe Web.cmd", StringComparison.Ordinal),
            "The not-installed message names the download page and the installer.");
        Console.WriteLine("[OK] launcher: registry value, else %LOCALAPPDATA%\\Programs\\Wireframe Web, else not installed");
    }

    private static void VerifyProjectCode()
    {
        var expected = new Dictionary<string, string>
        {
            [@"C:\P\MET - Wireframe.3dm"] = "MET",
            [@"V:\P00012\05-Wireframe\met - Wireframe LIVE TEST.3DM"] = "MET",
            [@"C:\P\BKT_old - Wireframe.3dm"] = "BKT",
            [@"C:\P\Wireframe.3dm"] = "",
            [@"C:\P\MET-Wireframe.3dm"] = "",
            [@"C:\P\ - Wireframe.3dm"] = "",
            [@"C:\P\_x - Wireframe.3dm"] = "",
            [@"C:\P\MET - Wireframe.3dmbak"] = "",
            [""] = "",
        };
        foreach ((string path, string code) in expected)
            Require(WireframeWebLauncher.ProjectCode(path) == code, $"Project code of '{path}' must be '{code}'.");
        Console.WriteLine("[OK] model name: \"<CODE> - ....3dm\" with the code before the first \" - \"");
    }

    private static void VerifyOpen()
    {
        string launcher = Path.Combine(Fresh("Wireframe Web"), WireframeWebLauncher.LauncherFileName);
        File.WriteAllText(launcher, FakeLauncher.ReplaceLineEndings("\r\n"));
        string model = Path.Combine(Fresh(@"Projects\A&B Tower"), "MET - Wireframe.3dm");
        File.WriteAllText(model, "");
        Environment.SetEnvironmentVariable("PCWEB_FAKE_LOG", Log);
        TimeSpan timeout = TimeSpan.FromSeconds(30);
        Directory.CreateDirectory(WireframeWebLauncher.OutputFolder);
        string stale = Path.Combine(WireframeWebLauncher.OutputFolder, "stale.out.txt");
        File.WriteAllText(stale, "");
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddDays(-2));

        WireframeWebResult ok = Run(launcher, model, "ok", timeout, out _);
        Require(ok.Success && ok.ExitCode == 0 && ok.Address == Address, $"Exit 0 returns the last stdout line: {ok}");
        Require(File.ReadAllLines(Log).Single() == $"--open \"{model}\"", "The launcher receives --open and the quoted model path.");
        Require(!File.Exists(stale), "Output files older than a day are removed.");

        foreach ((string mode, int code, string message) in new[]
                 {
                     ("bad", 1, "No project code in the file name."),
                     ("server", 2, "The server did not start."),
                     ("load", 3, "The model failed to load."),
                 })
        {
            WireframeWebResult failed = Run(launcher, model, mode, timeout, out _);
            Require(!failed.Success && failed.ExitCode == code && failed.Message == message && failed.Address.Length == 0,
                $"Exit {code} returns its stderr line: {failed}");
        }
        WireframeWebResult silent = Run(launcher, model, "silent", timeout, out _);
        Require(!silent.Success && silent.ExitCode == 2 && silent.Message == "Wireframe Web exited with code 2.",
            $"A failure without stderr names the exit code: {silent}");
        Console.WriteLine("[OK] fake launcher: --open \"<model>\" with spaces and '&'; exit 0 address; exits 1/2/3 messages");

        WireframeWebResult slow = Run(launcher, model, "slow", TimeSpan.FromSeconds(1), out TimeSpan slowTime);
        Require(!slow.Success && slow.ExitCode == -1 && slow.Message.Contains("did not answer", StringComparison.Ordinal) &&
                slowTime < TimeSpan.FromSeconds(3), $"A launcher past the timeout is reported: {slow} after {slowTime}");

        WireframeWebResult child = Run(launcher, model, "child", timeout, out TimeSpan childTime);
        Require(child.Success && child.Address == Address && childTime < TimeSpan.FromSeconds(5),
            $"A child that keeps the output open does not hold the result: {child} after {childTime}");

        WireframeWebResult missing = WireframeWebLauncher.Open(Path.Combine(Root, "missing.cmd"), model, timeout);
        Require(!missing.Success && missing.ExitCode == -1 && missing.Message.StartsWith("Wireframe Web could not be started", StringComparison.Ordinal),
            $"A launcher that cannot start is reported: {missing}");
        Console.WriteLine($"[OK] timeout reported after {slowTime.TotalSeconds:0.0} s; a child holding the output for 8 s " +
            $"returned after {childTime.TotalSeconds:0.0} s; unstartable launcher reported");
    }

    private static void VerifyCommandSource()
    {
        string source = File.ReadAllText(Path.Combine(
            RegressionPaths.RepositoryRoot, "src", "PanelCladdingEditor", "UI", "PanelCladdingWebCommand.cs"));
        int path = source.IndexOf("string.IsNullOrWhiteSpace(doc.Path)", StringComparison.Ordinal);
        int code = source.IndexOf("WireframeWebLauncher.ProjectCode(doc.Path)", StringComparison.Ordinal);
        int find = source.IndexOf("WireframeWebLauncher.FindLauncher()", StringComparison.Ordinal);
        int readOnly = source.IndexOf("if (doc.Modified && doc.IsReadOnly)", StringComparison.Ordinal);
        int modified = source.IndexOf("else if (doc.Modified)", StringComparison.Ordinal);
        int ask = source.IndexOf("askToSave(doc)", StringComparison.Ordinal);
        int save = source.IndexOf("doc.Save()", StringComparison.Ordinal);
        int run = source.IndexOf("Task.Run(", StringComparison.Ordinal);
        int open = source.IndexOf("WireframeWebLauncher.Open(", StringComparison.Ordinal);
        int write = source.IndexOf("RhinoApp.InvokeOnUiThread(", StringComparison.Ordinal);
        Require(new[] { path, code, find, readOnly, modified, ask, save, run, open, write }.All(i => i >= 0) &&
                path < code && code < find && find < readOnly && readOnly < modified && modified < ask && ask < save &&
                save < run && run < open && open < write,
            "PCweb checks the path, name and launcher, skips the question for a read-only model, asks about unsaved " +
            "changes, then launches on a worker thread and writes on the UI thread.");
        Require(source.Contains("ShowMessageButton.YesNoCancel", StringComparison.Ordinal) &&
                source.Contains("mode == RunMode.Interactive", StringComparison.Ordinal) &&
                source.Contains("AddOption(\"Save\")", StringComparison.Ordinal) &&
                source.Contains("AddOption(\"LastSave\")", StringComparison.Ordinal),
            "Interactive runs ask Yes/No/Cancel; scripted runs offer Save and LastSave.");
        // A launcher started with redirected output inherits Rhino's handles, including its lock on
        // the open model; a browser it starts then keeps Rhino from saving ("File Is Read-only").
        string launcher = File.ReadAllText(Path.Combine(
            RegressionPaths.RepositoryRoot, "src", "PanelCladdingEditor", "Infrastructure", "WireframeWeb", "WireframeWebLauncher.cs"));
        Require(launcher.Contains("UseShellExecute = true", StringComparison.Ordinal) &&
                !launcher.Contains("UseShellExecute = false", StringComparison.Ordinal) &&
                !launcher.Contains("RedirectStandard", StringComparison.Ordinal),
            "The launcher is started by the shell, so it inherits none of Rhino's handles.");
        Console.WriteLine("[OK] command source: checks, save question, worker-thread launch, result on the UI thread; " +
            "the launcher is started by the shell (no inherited handles)");
    }

    private static WireframeWebResult Run(string launcher, string model, string mode, TimeSpan timeout, out TimeSpan elapsed)
    {
        File.WriteAllText(Log, "");
        Environment.SetEnvironmentVariable("PCWEB_FAKE_MODE", mode);
        var watch = Stopwatch.StartNew();
        WireframeWebResult result = WireframeWebLauncher.Open(launcher, model, timeout);
        elapsed = watch.Elapsed;
        return result;
    }

    private static string Fresh(string name)
    {
        string path = Path.Combine(Root, name);
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        Directory.CreateDirectory(path);
        return path;
    }

    // Behaves like wireframe-web.cmd for each PCWEB_FAKE_MODE; "child" leaves a process holding
    // the output after the launcher exits, as a browser it starts may.
    private const string FakeLauncher = """
        @echo off
        >>"%PCWEB_FAKE_LOG%" echo(%*
        goto %PCWEB_FAKE_MODE%
        :ok
        echo Starting Wireframe Web...
        echo.
        echo http://127.0.0.1:8795/?source=api^&project=met^&user=tester
        exit /b 0
        :bad
        >&2 echo No project code in the file name.
        exit /b 1
        :server
        >&2 echo The server did not start.
        exit /b 2
        :load
        >&2 echo The model failed to load.
        exit /b 3
        :silent
        exit /b 2
        :slow
        ping -n 5 127.0.0.1 >nul
        exit /b 0
        :child
        start "" /b cmd /c "ping -n 9 127.0.0.1 >nul"
        echo http://127.0.0.1:8795/?source=api^&project=met^&user=tester
        exit /b 0
        """;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
