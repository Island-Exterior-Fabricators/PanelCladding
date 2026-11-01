[CmdletBinding()]
param(
    [string]$Version,
    [string]$OutputRoot,
    # Company-internal Yak source (network folder or private server). Omit to build only.
    [string]$Source,
    # Publish this already built .yak to -Source instead of building a new one.
    [string]$PackagePath,
    [string]$YakPath = (Join-Path $env:ProgramFiles 'Rhino 8\System\Yak.exe')
)

function Publish-YakPackage([string] $Package) {
    if ($Source -match '^https?://') {
        # A private Yak server applies its own authentication to yak push.
        & $YakPath push --source $Source $Package | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Yak push to $Source failed." }
    } else {
        # A folder source is read directly by Rhino; yak push would demand a Rhino Accounts login.
        if (-not (Test-Path -LiteralPath $Source -PathType Container)) { throw "Package source folder does not exist: $Source" }
        $target = Join-Path $Source ([IO.Path]::GetFileName($Package))
        if (Test-Path -LiteralPath $target -PathType Leaf) {
            # A published version is immutable: clients cache it by name and version.
            if ((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $Package).Hash) {
                throw "$target already exists with different content. Bump the version instead of replacing it."
            }
            Write-Host "Already published: $target"
            return
        }
        Copy-Item -LiteralPath $Package -Destination $target
    }
    Write-Host "Published to $Source"
}

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $PSScriptRoot "artifacts" }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (-not (Test-Path -LiteralPath $YakPath -PathType Leaf)) {
    throw "Rhino 8 Yak CLI was not found at $YakPath. Pass -YakPath."
}
if (-not [string]::IsNullOrWhiteSpace($Source) -and $Source -match '^https?://([^/]+\.)?rhino3d\.com') {
    # Pushing there publishes to every Rhino user; that is a deliberate manual step, not a build option.
    throw "Refusing to push to the public McNeel package server ($Source). Use a company-internal source."
}
if (-not [string]::IsNullOrWhiteSpace($PackagePath)) {
    if ([string]::IsNullOrWhiteSpace($Source)) { throw "-PackagePath publishes an existing package and needs -Source." }
    $PackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
    Publish-YakPackage $PackagePath
    Write-Output $PackagePath
    return
}
$definition = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "package-manifest.json") | ConvertFrom-Json

# Reuse the zip bundle build: publish, direct RHP rebuild, PE GUID check and dependency filtering.
$bundle = @(& (Join-Path $PSScriptRoot "Build-PanelCladdingEditorPackage.ps1") -Version $Version -OutputRoot $OutputRoot)[-1]
$bundle = [string]$bundle
$Version = (Get-Content -Raw -LiteralPath (Join-Path $bundle "package-manifest.json") | ConvertFrom-Json).version
$plugin = Join-Path $bundle "Plugin"
if (-not (Test-Path -LiteralPath (Join-Path $plugin "PanelCladdingEditor.rhp") -PathType Leaf)) {
    throw "Bundle build did not produce $plugin\PanelCladdingEditor.rhp."
}

$stage = [IO.Path]::GetFullPath((Join-Path $OutputRoot "yak\PanelCladdingEditor-$Version"))
if (-not $stage.StartsWith($OutputRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Generated path is outside the output root: $stage"
}
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -Path (Join-Path $plugin "*") -Destination $stage -Recurse -Force

# The guid keyword lets Rhino offer this package when an opened model references the plug-in.
@"
---
name: PanelCladdingEditor
version: $Version
authors:
  - Panel Cladding Editor contributors
description: Standalone Rhino 8 panel cladding type editor
url: https://github.com/nuofan-island/PanelCladding
keywords:
  - cladding
  - curtain-wall
  - panel
  - guid:$(([string]$definition.pluginId).ToLowerInvariant())
"@ | Set-Content -LiteralPath (Join-Path $stage "manifest.yml") -Encoding utf8

Push-Location -LiteralPath $stage
try {
    & $YakPath build --platform win | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Yak build failed." }
} finally { Pop-Location }
$built = @(Get-ChildItem -LiteralPath $stage -Filter "*.yak" -File)
if ($built.Count -ne 1) { throw "Expected one .yak file in $stage, found $($built.Count)." }
$package = Join-Path $OutputRoot $built[0].Name
Move-Item -LiteralPath $built[0].FullName -Destination $package -Force

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($package)
try { $entries = @($zip.Entries | ForEach-Object { $_.FullName.Replace('\', '/') }) } finally { $zip.Dispose() }
foreach ($required in @("manifest.yml", "PanelCladdingEditor.rhp", "PanelCladdingEditor.deps.json")) {
    if ($entries -notcontains $required) { throw "Yak package is missing $required." }
}
$unexpected = @($entries | Where-Object {
    $name = [IO.Path]::GetFileName($_)
    $name -in @("RhinoCommon.dll", "Rhino.UI.dll", "Eto.dll", "Eto.Wpf.dll", "PanelCladdingEditor.dll") -or
        $name.EndsWith(".pdb", [StringComparison]::OrdinalIgnoreCase) -or
        $name -match "^(MCP_Rhino|ModelContextProtocol|Microsoft\.Extensions\.Hosting)"
})
if ($unexpected) { throw "Yak package contains files it must not ship: $($unexpected -join ', ')" }

$checksum = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant()
"$checksum  $([IO.Path]::GetFileName($package))" | Set-Content -LiteralPath "$package.sha256" -Encoding ascii
Write-Host "Yak package: $package"
Write-Host "SHA-256: $checksum"

if (-not [string]::IsNullOrWhiteSpace($Source)) { Publish-YakPackage $package }
Write-Output $package
