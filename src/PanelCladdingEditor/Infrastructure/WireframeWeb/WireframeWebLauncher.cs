using System.Diagnostics;
using Microsoft.Win32;
using PanelCladdingEditor.Application.Services.PanelCladding;

namespace PanelCladdingEditor.Infrastructure.WireframeWeb;

// Opens a saved model in Wireframe Web, a separate per-user install, through its launch command:
// "<Launcher>" --open "<model>.3dm". The command starts Wireframe Web or reuses the running one,
// loads the file's latest save and opens a browser tab on the project. Exit 0 prints the page
// address as the last line of stdout; 1 (bad model), 2 (server did not start) and 3 (model failed
// to load) print one line on stderr. Nothing else of Wireframe Web is used.
public static class WireframeWebLauncher
{
    public const string RegistryKey = @"Software\IEF\Wireframe Web";
    public const string LauncherValue = "Launcher";
    public const string LauncherFileName = "wireframe-web.cmd";
    public const string DownloadPage = "https://github.com/nuofan-island/wireframe-web/releases/latest";
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    // Output files older than this are removed on the next run.
    private static readonly TimeSpan OutputLifetime = TimeSpan.FromDays(1);

    public static string NotInstalledMessage =>
        $"Wireframe Web is not installed. Download it from {DownloadPage} and run \"Install Wireframe Web.cmd\".";

    public static string OutputFolder => Path.Combine(Path.GetTempPath(), "PanelCladdingEditor", "PCweb");

    // The registry's Launcher, else the default install; null when neither file exists.
    public static string? FindLauncher() =>
        FindLauncher(ReadRegisteredLauncher(), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public static string? FindLauncher(string? registeredLauncher, string localAppData)
    {
        if (!string.IsNullOrWhiteSpace(registeredLauncher) && File.Exists(registeredLauncher.Trim()))
            return registeredLauncher.Trim();
        string fallback = Path.Combine(localAppData, "Programs", "Wireframe Web", LauncherFileName);
        return File.Exists(fallback) ? fallback : null;
    }

    // Wireframe Web needs "<CODE> - ….3dm": the project code is the text before the first " - ".
    // Returns the code, or empty when the name does not follow it.
    public static string ProjectCode(string? modelPath)
    {
        string path = (modelPath ?? string.Empty).Trim();
        if (!string.Equals(Path.GetExtension(path), ".3dm", StringComparison.OrdinalIgnoreCase)) return string.Empty;
        return Path.GetFileNameWithoutExtension(path).IndexOf(" - ", StringComparison.Ordinal) > 0
            ? PanelCladdingProjectService.CodeFromDocumentPath(path)
            : string.Empty;
    }

    // Runs the launcher and waits for it to exit (up to timeout). Blocks: call it off the UI thread.
    // A launcher still running after the timeout is left running.
    //
    // The shell starts cmd.exe, so the launcher inherits none of the caller's handles. Started
    // directly with redirected output it would inherit them all, and Rhino's handle on the open model
    // would pass on to the browser the launcher starts: while that lives, Rhino cannot save the model
    // ("File Is Read-only"). cmd.exe writes the output to files instead of pipes.
    public static WireframeWebResult Open(string launcher, string modelPath, TimeSpan timeout)
    {
        if (!File.Exists(launcher))
            return WireframeWebResult.Failed(-1, $"Wireframe Web could not be started: {launcher} is missing.");
        string output, error;
        try
        {
            Directory.CreateDirectory(OutputFolder);
            RemoveOldOutput();
            string run = Path.Combine(OutputFolder, Guid.NewGuid().ToString("N"));
            output = run + ".out.txt";
            error = run + ".err.txt";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return WireframeWebResult.Failed(-1, $"Wireframe Web could not be started: {ex.Message}");
        }

        var start = new ProcessStartInfo(
            Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            $"/d /s /c \"\"{launcher}\" --open \"{modelPath}\" 1>\"{output}\" 2>\"{error}\"\"")
        {
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        int exitCode;
        try
        {
            using Process? process = Process.Start(start);
            if (process is null)
                return WireframeWebResult.Failed(-1, "Wireframe Web could not be started.");
            if (!process.WaitForExit((int)Math.Min(timeout.TotalMilliseconds, int.MaxValue)))
                return WireframeWebResult.Failed(-1, $"Wireframe Web did not answer within {Describe(timeout)}.");
            exitCode = process.ExitCode;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return WireframeWebResult.Failed(-1, $"Wireframe Web could not be started: {ex.Message}");
        }

        string[] stdout = ReadLines(output);
        string[] stderr = ReadLines(error);
        if (exitCode == 0)
            return WireframeWebResult.Opened(stdout.LastOrDefault() ?? string.Empty);
        string message = string.Join(" ", stderr).Trim();
        return WireframeWebResult.Failed(exitCode, message.Length > 0 ? message : $"Wireframe Web exited with code {exitCode}.");
    }

    private static string? ReadRegisteredLauncher()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryKey);
        return key?.GetValue(LauncherValue) as string;
    }

    // Children of the launcher (the browser) may keep the file open, so it is read shared and
    // deleted when it can be; what is left is removed by a later run.
    private static string[] ReadLines(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            string[] lines = reader.ReadToEnd().Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
            reader.Dispose();
            TryDelete(path);
            return lines;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static void RemoveOldOutput()
    {
        foreach (string file in Directory.EnumerateFiles(OutputFolder, "*.txt"))
        {
            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) > OutputLifetime) TryDelete(file);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string Describe(TimeSpan span) =>
        span.TotalMinutes >= 1 ? $"{span.TotalMinutes:0.#} minute(s)" : $"{span.TotalSeconds:0.#} second(s)";
}

// ExitCode is the launcher's, or -1 when it could not be started or did not exit in time.
public sealed record WireframeWebResult(bool Success, int ExitCode, string Address, string Message)
{
    public static WireframeWebResult Opened(string address) => new(true, 0, address, string.Empty);

    public static WireframeWebResult Failed(int exitCode, string message) => new(false, exitCode, string.Empty, message);
}
