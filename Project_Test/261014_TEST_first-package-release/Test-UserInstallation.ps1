[CmdletBinding()]
param([Parameter(Mandatory)][string] $BundleRoot)
$ErrorActionPreference = 'Stop'
$BundleRoot = (Resolve-Path -LiteralPath $BundleRoot).Path
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this acceptance test from a non-elevated Windows account.'
}
Write-Output "PASS non-elevated token; PowerShell $($PSVersionTable.PSVersion)"
$definition = Get-Content -LiteralPath (Join-Path $BundleRoot 'package-manifest.json') -Raw | ConvertFrom-Json
$testId = [Guid]::NewGuid().ToString('N')
$testBase = Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor-ReleaseTests'
$testRoot = [IO.Path]::GetFullPath((Join-Path $testBase $testId))
if (-not $testRoot.StartsWith([IO.Path]::GetFullPath($testBase).TrimEnd('\') + '\')) { throw 'Unsafe fixture path.' }
$productRoot = Join-Path $testRoot 'Product with spaces'
$pluginBase = Join-Path $productRoot 'plugin'
$legacyRoot = Join-Path $testRoot 'Legacy packages'
$registryCleanup = "Registry::HKEY_CURRENT_USER\Software\PanelCladdingEditor\ReleaseTests\$testId"
$registryRoot = "$registryCleanup\Plug-Ins"
$pluginKey = "$registryRoot\$(([string]$definition.pluginId).ToLowerInvariant())"
$rhp = Join-Path (Join-Path $pluginBase $definition.version) 'PanelCladdingEditor.rhp'
$ownershipPath = Join-Path $productRoot 'install-manifest.json'
$installer = Join-Path $BundleRoot 'Installer\Install-PanelCladdingEditor.ps1'
$uninstaller = Join-Path $BundleRoot 'Installer\Uninstall-PanelCladdingEditor.ps1'
$shell = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
$installArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $installer,
    '-BundleRoot', $BundleRoot, '-ProductRoot', $productRoot, '-RhinoPluginRoot', $pluginBase,
    '-LegacyRhinoPackageRoot', $legacyRoot, '-RhinoPluginRegistryRoot', $registryRoot)
$commands = @('PCClear','PCCreate','PCCrvTemplate','PCEditor','PCMatchCrv','PCMatchSrf',
    'PCpid','PCSpawnCrv','PCSpawnSrf','PCSyncCrv','PCSyncSrf','PCUpdate')

Add-Type @'
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
public static class ReleaseRegistryTime {
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode)]
    static extern int RegQueryInfoKey(SafeRegistryHandle key, IntPtr cls, IntPtr clsLen,
        IntPtr reserved, IntPtr subKeys, IntPtr maxSubKey, IntPtr maxClass,
        IntPtr values, IntPtr maxValueName, IntPtr maxValueLen, IntPtr security, out long time);
    public static long Read(RegistryKey key) {
        long time;
        int error = RegQueryInfoKey(key.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
            IntPtr.Zero, out time);
        if (error != 0) throw new System.ComponentModel.Win32Exception(error);
        return time;
    }
}
'@

function Require([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
    Write-Output "PASS $Message"
}
function Run-Install([string] $Mode, [bool] $ShouldPass = $true) {
    # Each invocation exits before the next process validates or reads registry data.
    $exitCode = Run-Process ($installArgs + @('-Mode', $Mode)) "$Mode.log"
    if (($exitCode -eq 0) -ne $ShouldPass) { Get-Content (Join-Path $testRoot "$Mode.log") | Write-Output }
    Require (($exitCode -eq 0) -eq $ShouldPass) "$Mode expected success=$ShouldPass"
}
function Run-Process([string[]] $Arguments, [string] $Log) {
    # Windows PowerShell emits NativeCommandError for redirected stderr, including
    # deliberately rejected inputs. Inspect the native exit code explicitly.
    $ErrorActionPreference = 'Continue'
    & $shell @Arguments *> (Join-Path $testRoot $Log)
    return $LASTEXITCODE
}
function Read-Registration {
    $root = Get-ItemProperty -LiteralPath $pluginKey
    Require ([int]$root.DirectoryInstall -eq 0 -and [int]$root.LoadMode -eq 1) 'registry-only startup registration'
    Require ([string]::IsNullOrEmpty([string]$root.FileName)) 'no shorthand root FileName'
    Require ((Get-ItemProperty -LiteralPath "$pluginKey\PlugIn").FileName -eq $rhp) 'exact PlugIn FileName after child process exits'
    $commandKey = Get-Item -LiteralPath "$pluginKey\CommandList"
    try {
        Require (@(Compare-Object $commands @($commandKey.GetValueNames())).Count -eq 0) 'exact twelve command names'
        foreach ($command in $commands) {
            if ($commandKey.GetValue($command) -cne "2;$command") { throw "Incorrect command value: $command" }
        }
    } finally { $commandKey.Close() }
    $times = @()
    foreach ($keyPath in @($pluginKey, "$pluginKey\PlugIn", "$pluginKey\CommandList")) {
        $key = Get-Item -LiteralPath $keyPath
        try { $times += [ReleaseRegistryTime]::Read($key) } finally { $key.Close() }
    }
    if (@($times | Where-Object { $_ -lt $script:operationStart }).Count -gt 0) {
        throw 'Registration timestamp predates installation/repair.'
    }
    Write-Output "PASS independent root/PlugIn/CommandList timestamps: $($times -join ', ')"
    $nativePath = $pluginKey.Replace('Registry::HKEY_CURRENT_USER', 'HKCU')
    & "$env:SystemRoot\System32\reg.exe" query "$nativePath\PlugIn" /v FileName > (Join-Path $testRoot 'reg-query.log')
    Require ($LASTEXITCODE -eq 0 -and (Get-Content -Raw (Join-Path $testRoot 'reg-query.log')).Contains($rhp)) 'reg.exe confirms child RHP pointer'
}
try {
    New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
    $script:operationStart = [DateTime]::UtcNow.ToFileTimeUtc()
    Run-Install Install
    Run-Install Validate
    Read-Registration
    $beforeFiles = @(Get-ChildItem $productRoot -Recurse -File | ForEach-Object { "$($_.FullName)|$((Get-FileHash $_.FullName).Hash)" })
    $exitCode = Run-Process @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $uninstaller, '-ProductRoot', $productRoot, '-WhatIf') 'WhatIf.log'
    if ($exitCode -ne 0) { Get-Content (Join-Path $testRoot 'WhatIf.log') | Write-Output }
    Require ($exitCode -eq 0) 'uninstall WhatIf succeeds'
    $afterFiles = @(Get-ChildItem $productRoot -Recurse -File | ForEach-Object { "$($_.FullName)|$((Get-FileHash $_.FullName).Hash)" })
    Require (@(Compare-Object $beforeFiles $afterFiles).Count -eq 0 -and (Test-Path $pluginKey)) 'WhatIf preserves files, manifest and registration'
    Run-Install Validate

    Remove-ItemProperty -LiteralPath "$pluginKey\CommandList" -Name PCEditor
    Run-Install Validate $false
    $script:operationStart = [DateTime]::UtcNow.ToFileTimeUtc()
    Run-Install Repair
    Run-Install Validate
    Read-Registration

    Set-ItemProperty -LiteralPath "$pluginKey\PlugIn" -Name FileName -Value (Join-Path $testRoot 'missing\PanelCladdingEditor.rhp')
    Run-Install Validate $false
    $script:operationStart = [DateTime]::UtcNow.ToFileTimeUtc()
    Run-Install Repair
    Run-Install Validate
    Read-Registration

    $badArgs = @($installArgs)
    $badArgs[$badArgs.Length - 1] = 'Registry::HKEY_LOCAL_MACHINE\Software\PanelCladdingEditorReleaseTest'
    $exitCode = Run-Process ($badArgs + @('-Mode', 'Install')) 'HKLM-rejection.log'
    Require ($exitCode -ne 0 -and (Get-Content -Raw (Join-Path $testRoot 'HKLM-rejection.log')).Contains('HKEY_CURRENT_USER')) 'HKLM rejected before installation'
    Run-Install Validate

    # Force a failure after the replacement RHP and registration have been written.
    # The previous manifest remains readable but cannot be overwritten.
    $priorRhpHash = (Get-FileHash -LiteralPath $rhp).Hash
    $manifestLock = [IO.File]::Open($ownershipPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try { Run-Install Repair $false } finally { $manifestLock.Dispose() }
    Run-Install Validate
    Read-Registration
    Require ((Get-FileHash -LiteralPath $rhp).Hash -eq $priorRhpHash) 'failed repair restores prior files, registration and usable ownership manifest'

    $exitCode = Run-Process @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $uninstaller, '-ProductRoot', $productRoot) 'Uninstall.log'
    if ($exitCode -ne 0) { Get-Content (Join-Path $testRoot 'Uninstall.log') | Write-Output }
    Require ($exitCode -eq 0 -and -not (Test-Path $pluginKey) -and -not (Test-Path $rhp) -and -not (Test-Path $ownershipPath)) 'complete registration, RHP and manifest removed without elevation'

    Run-Install Install
    Run-Install Validate
    $deps = Join-Path (Split-Path $rhp) 'PanelCladdingEditor.deps.json'
    Add-Content -LiteralPath $deps -Value ' '
    $foreignRhp = Join-Path $testRoot 'OtherProduct.rhp'
    Set-ItemProperty -LiteralPath "$pluginKey\PlugIn" -Name FileName -Value $foreignRhp
    Run-Install Repair $false
    Require ((Test-Path $rhp) -and (Get-ItemProperty "$pluginKey\PlugIn").FileName -eq $foreignRhp) 'refused repair preserves existing files and foreign registration'
    $exitCode = Run-Process @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $uninstaller, '-ProductRoot', $productRoot) 'Preservation.log'
    Require ($exitCode -eq 0 -and (Test-Path $deps) -and (Test-Path $ownershipPath)) 'modified files and ownership evidence retained'
    Require ((Get-ItemProperty "$pluginKey\PlugIn").FileName -eq $foreignRhp) 'unowned registration retained'
    Write-Output 'PASS non-admin installation lifecycle; production registration was not modified.'
}
catch {
    Write-Output $_.ScriptStackTrace
    throw
}
finally {
    if (Test-Path -LiteralPath $registryCleanup) { Remove-Item -LiteralPath $registryCleanup -Recurse -Force }
    $resolved = [IO.Path]::GetFullPath($testRoot)
    if (-not $resolved.StartsWith([IO.Path]::GetFullPath($testBase).TrimEnd('\') + '\')) { throw 'Unsafe cleanup path.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
