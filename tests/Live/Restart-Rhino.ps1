param(
    [int] $ProcessId,
    [string] $Model,
    [switch] $CloseOnly,
    [int] $TimeoutSeconds = 180
)

Add-Type @"
using System; using System.Runtime.InteropServices;
public static class RhinoWindow { [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l); }
"@

$routes = Join-Path $env:LOCALAPPDATA 'MCP_Rhino\Routing\v1'
if ($ProcessId) {
    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($process) {
        $log = Join-Path $env:TEMP "restart-rhino-dialogs-$ProcessId.json"
        $watcher = Start-Process -FilePath powershell.exe -WindowStyle Hidden -PassThru -ArgumentList @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'Watch-Dialogs.ps1'),
            '-ProcessId', $ProcessId, '-Answers', "No|Don't Save|Do not save", '-Log', $log, '-TimeoutSeconds', 90)
        Start-Sleep -Seconds 2
        [void][RhinoWindow]::PostMessage($process.MainWindowHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)
        if (-not $process.WaitForExit(90000)) { throw "Rhino $ProcessId did not close; see $log" }
        if (-not $watcher.HasExited) { Stop-Process -Id $watcher.Id }
        "Rhino $ProcessId closed normally"
        if (Test-Path $log) { Get-Content $log -Raw }
    }
}
if ($CloseOnly) { return }

$started = Start-Process -FilePath 'C:\Program Files\Rhino 8\System\Rhino.exe' -ArgumentList '/nosplash', ('"' + $Model + '"') -PassThru
"Started Rhino $($started.Id) with $Model"
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ((Get-Date) -lt $deadline) {
    $route = Get-ChildItem $routes -Filter "$($started.Id)_*.json" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($route) { "MCP_Rhino route published: $($route.Name)"; return }
    if ($started.HasExited) { throw "Rhino $($started.Id) exited with code $($started.ExitCode)" }
    Start-Sleep -Seconds 2
}
throw "No MCP_Rhino route for Rhino $($started.Id) after $TimeoutSeconds s"
