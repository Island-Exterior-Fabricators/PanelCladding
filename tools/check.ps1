[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $repo

$failed = New-Object System.Collections.Generic.List[string]
$rhinoCommon = 'C:\Program Files\Rhino 8\System\netcore\RhinoCommon.dll'
$powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'

function Invoke-Step([string] $Name, [scriptblock] $Body) {
    Write-Host ''
    Write-Host "== $Name"
    $global:LASTEXITCODE = 0
    $passed = $true
    try {
        & $Body | Out-Host
        if ($LASTEXITCODE -ne 0) { $passed = $false }
    }
    catch {
        Write-Host $_.Exception.Message
        $passed = $false
    }
    if (-not $passed) {
        Write-Host "FAILED: $Name"
        $failed.Add($Name)
    }
    return $passed
}

function Invoke-Script([string] $Path, [string[]] $Arguments) {
    & $powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $Path @Arguments
}

function Complete-Check {
    Write-Host ''
    if ($failed.Count -gt 0) {
        Write-Host "Check failed: $($failed -join '; ')"
        exit 1
    }
    Write-Host 'All checks passed.'
    exit 0
}

$checkHome = Join-Path $repo 'artifacts\check-home'
foreach ($folder in @(@('LOCALAPPDATA', 'LocalAppData'), @('APPDATA', 'AppData'))) {
    if (-not [Environment]::GetEnvironmentVariable($folder[0])) {
        $path = Join-Path $checkHome $folder[1]
        New-Item -ItemType Directory -Force -Path $path | Out-Null
        [Environment]::SetEnvironmentVariable($folder[0], $path)
        Write-Host "$($folder[0]) is not set; using $path"
    }
}
if (-not $env:USERPROFILE) {
    foreach ($folder in @(@('DOTNET_CLI_HOME', 'DotnetHome'), @('NUGET_PACKAGES', 'NuGetPackages'))) {
        if (-not [Environment]::GetEnvironmentVariable($folder[0])) {
            $path = Join-Path $checkHome $folder[1]
            New-Item -ItemType Directory -Force -Path $path | Out-Null
            [Environment]::SetEnvironmentVariable($folder[0], $path)
            Write-Host "USERPROFILE is not set; $($folder[0]) is $path"
        }
    }
}
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'

$env:RestoreLockedMode = 'true'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

$toolchain = Invoke-Step 'Pinned .NET SDK (global.json, tools/dotnet-sdk.json)' { & (Join-Path $PSScriptRoot 'use-dotnet-sdk.ps1') }
$rhino = Invoke-Step 'Rhino 8 reference assemblies' {
    if (-not (Test-Path -LiteralPath $rhinoCommon -PathType Leaf)) {
        throw "Rhino 8 is not installed: the projects reference $rhinoCommon."
    }
    Write-Output "RhinoCommon: $rhinoCommon"
}

Invoke-Step 'Client data guard (.githooks/Test-RepositoryContent.ps1 -Mode Tracked)' {
    Invoke-Script (Join-Path $repo '.githooks\Test-RepositoryContent.ps1') @('-Mode', 'Tracked')
} | Out-Null
Invoke-Step 'Standalone references and plug-in GUID text (tests/Checks/Test-StandaloneReferences.ps1)' {
    Invoke-Script (Join-Path $repo 'tests\Checks\Test-StandaloneReferences.ps1') @()
} | Out-Null
Invoke-Step 'Viewport highlight drawing contract (tests/Checks/Test-ViewportHighlightDrawing.ps1)' {
    Invoke-Script (Join-Path $repo 'tests\Checks\Test-ViewportHighlightDrawing.ps1') @()
} | Out-Null

if (-not ($toolchain -and $rhino)) {
    Write-Host ''
    Write-Host 'Skipping the builds and smokes: the tool chain or Rhino 8 is missing.'
    Complete-Check
}

Invoke-Step 'No comments, one sentence per line, no timed waits in tests (tools/RepoCheck)' {
    & dotnet build tools/RepoCheck/RepoCheck.csproj -c Release -warnaserror --nologo -v:q
    if ($LASTEXITCODE -ne 0) { return }
    & dotnet (Join-Path $repo 'tools\RepoCheck\bin\Release\net8.0\RepoCheck.dll') $repo
} | Out-Null

Invoke-Step 'Banned time APIs fail a test build (tests/Checks/Test-BannedTestApis.ps1)' {
    Invoke-Script (Join-Path $repo 'tests\Checks\Test-BannedTestApis.ps1') @()
} | Out-Null

foreach ($configuration in 'Debug', 'Release') {
    Invoke-Step "Build PanelCladding.sln ($configuration, warnings as errors, locked restore)" {
        & dotnet build PanelCladding.sln -c $configuration -m:1 -warnaserror --nologo -v:m
    } | Out-Null
    Invoke-Step "Plug-in assembly identity ($configuration RHP)" {
        Invoke-Script (Join-Path $repo 'tests\Package\Verify-PluginAssemblyIdentity.ps1') @('-Configuration', $configuration)
    } | Out-Null
}

Invoke-Step 'Managed smokes, Debug and Release (tests/Run-Regression.ps1)' {
    Invoke-Script (Join-Path $repo 'tests\Run-Regression.ps1') @()
} | Out-Null

Complete-Check
