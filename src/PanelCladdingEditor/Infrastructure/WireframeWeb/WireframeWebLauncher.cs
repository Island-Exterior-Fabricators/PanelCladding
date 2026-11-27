using System.Diagnostics;
using Microsoft.Win32;
using PanelCladdingEditor.Application.Services.PanelCladding;

namespace PanelCladdingEditor.Infrastructure.WireframeWeb;

public static class WireframeWebLauncher
{
    public const string RegistryKey = @"Software\IEF\Wireframe Web";
    public const string LauncherValue = "Launcher";
    public const string LauncherFileName = "wireframe-web.cmd";
    public const string DownloadPage = "https://github.com/Island-Exterior-Fabricators/wireframe-web/releases/latest";
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    private static readonly TimeSpan OutputLifetime = TimeSpan.FromDays(1);

    public static string NotInstalledMessage =>
        $"Wireframe Web is not installed. Download it from {DownloadPage} and run \"Install Wireframe Web.cmd\".";

    public static string OutputFolder => Path.Combine(Path.GetTempPath(), "PanelCladdingEditor", "PCweb");

    public static string? FindLauncher() =>
        FindLauncher(ReadRegisteredLauncher(), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public static string? FindLauncher(string? registeredLauncher, string localAppData)
    {
        if (!string.IsNullOrWhiteSpace(registeredLauncher) && File.Exists(registeredLauncher.Trim()))
            return registeredLauncher.Trim();
        string fallback = Path.Combine(localAppData, "Programs", "Wireframe Web", LauncherFileName);
        return File.Exists(fallback) ? fallback : null;
    }

    public static string ProjectCode(string? modelPath)
    {
        string path = (modelPath ?? string.Empty).Trim();
        if (!string.Equals(Path.GetExtension(path), ".3dm", StringComparison.OrdinalIgnoreCase)) return string.Empty;
        return Path.GetFileNameWithoutExtension(path).IndexOf(" - ", StringComparison.Ordinal) > 0
            ? PanelCladdingProjectService.CodeFromDocumentPath(path)
            : string.Empty;
    }

    public static WireframeWebResult Open(string launcher, string modelPath, TimeSpan timeout) =>
        Open(launcher, modelPath, timeout, TimeProvider.System);

    public static WireframeWebResult Open(string launcher, string modelPath, TimeSpan timeout, TimeProvider clock)
    {
        if (!File.Exists(launcher))
            return WireframeWebResult.Failed(-1, $"Wireframe Web could not be started: {launcher} is missing.");
        string output, error;
        try
        {
            Directory.CreateDirectory(OutputFolder);
            RemoveOldOutput(clock.GetUtcNow().UtcDateTime);
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
            using var stop = new CancellationTokenSource();
            Task exited = process.WaitForExitAsync(stop.Token);
            Task expired = Task.Delay(timeout, clock, stop.Token);
            bool answered = Task.WaitAny(exited, expired) == 0;
            stop.Cancel();
            if (!answered)
                return WireframeWebResult.Failed(-1, $"Wireframe Web did not answer within {Describe(timeout)}.");
            process.WaitForExit();
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

    private static void RemoveOldOutput(DateTime nowUtc)
    {
        foreach (string file in Directory.EnumerateFiles(OutputFolder, "*.txt"))
        {
            if (nowUtc - File.GetLastWriteTimeUtc(file) > OutputLifetime) TryDelete(file);
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

public sealed record WireframeWebResult(bool Success, int ExitCode, string Address, string Message)
{
    public static WireframeWebResult Opened(string address) => new(true, 0, address, string.Empty);

    public static WireframeWebResult Failed(int exitCode, string message) => new(false, exitCode, string.Empty, message);
}
