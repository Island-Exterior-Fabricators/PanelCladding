[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $ProductRoot = (Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor')
)

$ErrorActionPreference = 'Stop'
function Get-InstalledFileHash([string] $Path) {
    # Hash directly: Windows PowerShell 5.1's Get-FileHash can suppress its
    # internal ProviderPath read when the script is launched with -WhatIf.
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        $stream = [IO.File]::OpenRead($Path)
        try {
            return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
        } finally { $stream.Dispose() }
    } finally { $algorithm.Dispose() }
}
$ProductRoot = [IO.Path]::GetFullPath($ProductRoot)
$manifestPath = Join-Path $ProductRoot 'install-manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "No installer-owned PanelCladdingEditor manifest exists at $manifestPath."
}
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
if ($manifest.product -ne 'PanelCladdingEditor' -or $manifest.schemaVersion -ne 1) {
    throw 'Unsupported PanelCladdingEditor ownership manifest.'
}

$defaultProductRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor'))
if ($ProductRoot.Equals($defaultProductRoot, [StringComparison]::OrdinalIgnoreCase) -and
    @(Get-Process -Name Rhino -ErrorAction SilentlyContinue).Count -gt 0) {
    throw 'Close every Rhino window before uninstalling PanelCladdingEditor.'
}

$registryRoot = [string]$manifest.rhinoPluginRegistryRoot
if (-not $registryRoot.StartsWith('Registry::HKEY_CURRENT_USER\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The recorded Rhino registry root is outside HKEY_CURRENT_USER.'
}
$pluginKey = Join-Path $registryRoot ([string]$manifest.pluginId).ToLowerInvariant()
$ownedRhp = Join-Path ([string]$manifest.pluginRoot) 'PanelCladdingEditor.rhp'
if (Test-Path -LiteralPath $pluginKey) {
    $root = Get-ItemProperty -LiteralPath $pluginKey -ErrorAction SilentlyContinue
    $child = Get-ItemProperty -LiteralPath (Join-Path $pluginKey 'PlugIn') -ErrorAction SilentlyContinue
    $rootPath = if ($null -ne $root) { [string]$root.FileName } else { '' }
    $childPath = if ($null -ne $child) { [string]$child.FileName } else { '' }
    $owned = $null -ne $root -and $null -ne $child -and
        ([string]$root.Name).Equals('PanelCladdingEditor', [StringComparison]::OrdinalIgnoreCase) -and
        -not [string]::IsNullOrWhiteSpace($childPath) -and
        ([string]::IsNullOrWhiteSpace($rootPath) -or
         ([IO.Path]::GetFullPath($rootPath)).Equals([IO.Path]::GetFullPath($ownedRhp), [StringComparison]::OrdinalIgnoreCase)) -and
        ([IO.Path]::GetFullPath($childPath)).Equals([IO.Path]::GetFullPath($ownedRhp), [StringComparison]::OrdinalIgnoreCase)
    if ($owned) {
        if ($PSCmdlet.ShouldProcess($pluginKey, 'Remove installer-owned Rhino plug-in registration')) {
            Remove-Item -LiteralPath $pluginKey -Recurse -Force
        }
    } else {
        Write-Warning "Rhino plug-in registration changed after installation and was left untouched: $pluginKey"
    }
}

$retained = @()
foreach ($file in @($manifest.files)) {
    if (-not (Test-Path -LiteralPath $file.path -PathType Leaf)) { continue }
    $actual = Get-InstalledFileHash ([string]$file.path)
    if ($actual -ne [string]$file.sha256) {
        $retained += [string]$file.path
        Write-Warning "Modified installer-owned file was left untouched: $($file.path)"
        continue
    }
    if ($PSCmdlet.ShouldProcess([string]$file.path, 'Remove installer-owned plug-in file')) {
        Remove-Item -LiteralPath $file.path -Force
    }
}

$cleanupRoots = @(
    [string]$manifest.pluginRoot,
    (Split-Path -Parent ([string]$manifest.pluginRoot))
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object { $_.Length } -Descending -Unique
foreach ($root in $cleanupRoots) {
    if (Test-Path -LiteralPath $root -PathType Container) {
        Get-ChildItem -LiteralPath $root -Directory -Recurse -ErrorAction SilentlyContinue |
            Sort-Object { $_.FullName.Length } -Descending | ForEach-Object {
                if (-not (Get-ChildItem -LiteralPath $_.FullName -Force | Select-Object -First 1)) {
                    if ($PSCmdlet.ShouldProcess($_.FullName, 'Remove empty plug-in directory')) {
                        Remove-Item -LiteralPath $_.FullName
                    }
                }
            }
    }
    if ((Test-Path -LiteralPath $root -PathType Container) -and
        -not (Get-ChildItem -LiteralPath $root -Force | Select-Object -First 1)) {
        if ($PSCmdlet.ShouldProcess($root, 'Remove empty plug-in directory')) {
            Remove-Item -LiteralPath $root
        }
    }
}

if ($retained.Count -eq 0) {
    if ($PSCmdlet.ShouldProcess($manifestPath, 'Remove installation ownership manifest')) {
        Remove-Item -LiteralPath $manifestPath -Force
        Write-Output 'PanelCladdingEditor installer-owned registration and files were removed. Migration backups were retained.'
    }
} else {
    Write-Warning 'Uninstall retained modified files and the ownership manifest for manual review.'
}
