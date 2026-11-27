using System.IO.Pipes;
using PanelCladdingEditor.Infrastructure.WireframeWeb;

namespace PcWebCommandSmoke;

internal static class Program
{
    private const string Address = "http://127.0.0.1:8795/?source=api&project=xyz&user=tester";
    private static readonly string Root = RegressionPaths.Output("pcweb-command");
    private static readonly string Log = Path.Combine(Root, "launcher-calls.txt");
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

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
            [@"C:\P\XYZ - Wireframe.3dm"] = "XYZ",
            [@"V:\P00002\05-Wireframe\xyz - Wireframe LIVE TEST.3DM"] = "XYZ",
            [@"C:\P\ABC_old - Wireframe.3dm"] = "ABC",
            [@"C:\P\Wireframe.3dm"] = "",
            [@"C:\P\XYZ-Wireframe.3dm"] = "",
            [@"C:\P\ - Wireframe.3dm"] = "",
            [@"C:\P\_x - Wireframe.3dm"] = "",
            [@"C:\P\XYZ - Wireframe.3dmbak"] = "",
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
        string model = Path.Combine(Fresh(@"Projects\A&B Tower"), "XYZ - Wireframe.3dm");
        File.WriteAllText(model, "");
        Environment.SetEnvironmentVariable("PCWEB_FAKE_LOG", Log);
        TimeSpan timeout = TimeSpan.FromSeconds(30);
        var clock = new ManualTimeProvider(Now);
        Directory.CreateDirectory(WireframeWebLauncher.OutputFolder);
        string stale = Path.Combine(WireframeWebLauncher.OutputFolder, "stale.out.txt");
        string recent = Path.Combine(WireframeWebLauncher.OutputFolder, "recent.out.txt");
        File.WriteAllText(stale, "");
        File.SetLastWriteTimeUtc(stale, Now.UtcDateTime.AddDays(-2));
        File.WriteAllText(recent, "");
        File.SetLastWriteTimeUtc(recent, Now.UtcDateTime.AddHours(-1));

        WireframeWebResult ok = Run(launcher, model, "ok", timeout, clock);
        Require(ok.Success && ok.ExitCode == 0 && ok.Address == Address, $"Exit 0 returns the last stdout line: {ok}");
        Require(File.ReadAllLines(Log).Single() == $"--open \"{model}\"", "The launcher receives --open and the quoted model path.");
        Require(!File.Exists(stale), "Output files older than a day are removed.");
        Require(File.Exists(recent), "Output files younger than a day are kept.");
        File.Delete(recent);

        foreach ((string mode, int code, string message) in new[]
                 {
                     ("bad", 1, "No project code in the file name."),
                     ("server", 2, "The server did not start."),
                     ("load", 3, "The model failed to load."),
                 })
        {
            WireframeWebResult failed = Run(launcher, model, mode, timeout, clock);
            Require(!failed.Success && failed.ExitCode == code && failed.Message == message && failed.Address.Length == 0,
                $"Exit {code} returns its stderr line: {failed}");
        }
        WireframeWebResult silent = Run(launcher, model, "silent", timeout, clock);
        Require(!silent.Success && silent.ExitCode == 2 && silent.Message == "Wireframe Web exited with code 2.",
            $"A failure without stderr names the exit code: {silent}");
        Console.WriteLine("[OK] fake launcher: --open \"<model>\" with spaces and '&'; exit 0 address; exits 1/2/3 messages");

        string slowPipe = PipeName();
        using (var hold = new NamedPipeServerStream(slowPipe, PipeDirection.Out))
        {
            Task connected = hold.WaitForConnectionAsync();
            var slowClock = new ManualTimeProvider(Now);
            Environment.SetEnvironmentVariable("PCWEB_FAKE_PIPE", slowPipe);
            Task<WireframeWebResult> pending = Task.Run(() => Run(launcher, model, "slow", timeout, slowClock));
            connected.Wait();
            slowClock.WaitForTimer();
            slowClock.Advance(timeout);
            WireframeWebResult slow = pending.Result;
            Require(!slow.Success && slow.ExitCode == -1 && slow.Message == "Wireframe Web did not answer within 30 second(s).",
                $"A launcher still running when the clock passes the timeout is reported: {slow}");
            Require(hold.IsConnected, "The timed-out launcher is left running.");
        }

        string childPipe = PipeName();
        using (var hold = new NamedPipeServerStream(childPipe, PipeDirection.Out))
        {
            Task connected = hold.WaitForConnectionAsync();
            Environment.SetEnvironmentVariable("PCWEB_FAKE_PIPE", childPipe);
            WireframeWebResult child = Run(launcher, model, "child", timeout, new ManualTimeProvider(Now));
            connected.Wait();
            Require(child.Success && child.Address == Address && hold.IsConnected,
                $"A child that keeps the output open does not hold the result: {child}");
        }

        WireframeWebResult missing = WireframeWebLauncher.Open(Path.Combine(Root, "missing.cmd"), model, timeout, clock);
        Require(!missing.Success && missing.ExitCode == -1 && missing.Message.StartsWith("Wireframe Web could not be started", StringComparison.Ordinal),
            $"A launcher that cannot start is reported: {missing}");
        Console.WriteLine("[OK] timeout reported when the clock passes it, the launcher left running; a child holding the output " +
            "does not hold the result; unstartable launcher reported");
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
        string launcher = File.ReadAllText(Path.Combine(
            RegressionPaths.RepositoryRoot, "src", "PanelCladdingEditor", "Infrastructure", "WireframeWeb", "WireframeWebLauncher.cs"));
        Require(launcher.Contains("UseShellExecute = true", StringComparison.Ordinal) &&
                !launcher.Contains("UseShellExecute = false", StringComparison.Ordinal) &&
                !launcher.Contains("RedirectStandard", StringComparison.Ordinal),
            "The launcher is started by the shell, so it inherits none of Rhino's handles.");
        Console.WriteLine("[OK] command source: checks, save question, worker-thread launch, result on the UI thread; " +
            "the launcher is started by the shell (no inherited handles)");
    }

    private static WireframeWebResult Run(string launcher, string model, string mode, TimeSpan timeout, TimeProvider clock)
    {
        File.WriteAllText(Log, "");
        Environment.SetEnvironmentVariable("PCWEB_FAKE_MODE", mode);
        return WireframeWebLauncher.Open(launcher, model, timeout, clock);
    }

    private static string PipeName() => "pcweb-smoke-" + Guid.NewGuid().ToString("N");

    private static string Fresh(string name)
    {
        string path = Path.Combine(Root, name);
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        Directory.CreateDirectory(path);
        return path;
    }

    private const string FakeLauncher = """
        @echo off
        >>"%PCWEB_FAKE_LOG%" echo(%*
        goto %PCWEB_FAKE_MODE%
        :ok
        echo Starting Wireframe Web...
        echo.
        echo http://127.0.0.1:8795/?source=api^&project=xyz^&user=tester
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
        type "\\.\pipe\%PCWEB_FAKE_PIPE%" >nul
        exit /b 0
        :child
        start "" /b cmd /d /c "type "\\.\pipe\%PCWEB_FAKE_PIPE%" >nul"
        echo http://127.0.0.1:8795/?source=api^&project=xyz^&user=tester
        exit /b 0
        """;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
