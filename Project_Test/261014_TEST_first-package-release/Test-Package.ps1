[CmdletBinding()]
param([Parameter(Mandatory)][string] $Archive)
$ErrorActionPreference = 'Stop'
$Archive = (Resolve-Path -LiteralPath $Archive).Path
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$checksumLine = (Get-Content -LiteralPath "$Archive.sha256" -Raw).Trim()
$hash = (Get-FileHash -LiteralPath $Archive -Algorithm SHA256).Hash.ToLowerInvariant()
if ($checksumLine -cne "$hash  $([IO.Path]::GetFileName($Archive))") { throw 'ZIP checksum mismatch.' }
$testRoot = Join-Path $repo ("artifacts\release-verify-" + [Guid]::NewGuid().ToString('N'))
Expand-Archive -LiteralPath $Archive -DestinationPath $testRoot
try {
    $bundle = @(Get-ChildItem -LiteralPath $testRoot -Directory)
    if ($bundle.Count -ne 1) { throw 'Archive must contain one bundle directory.' }
    $bundleRoot = $bundle[0].FullName
    $manifest = Get-Content -Raw -LiteralPath (Join-Path $bundleRoot 'bundle-manifest.json') | ConvertFrom-Json
    $definition = Get-Content -Raw -LiteralPath (Join-Path $bundleRoot 'package-manifest.json') | ConvertFrom-Json
    if ($manifest.version -ne $definition.version -or $manifest.name -ne 'PanelCladdingEditor') { throw 'Bundle identity mismatch.' }
    $expectedPaths = @($manifest.files | ForEach-Object { [string]$_.path })
    if (@($expectedPaths | Sort-Object -Unique).Count -ne $expectedPaths.Count) { throw 'Duplicate bundle paths.' }
    $actualPaths = @(Get-ChildItem -LiteralPath $bundleRoot -File -Recurse | ForEach-Object {
        $_.FullName.Substring($bundleRoot.Length + 1).Replace('\', '/')
    } | Where-Object { $_ -ne 'bundle-manifest.json' })
    if (@(Compare-Object $expectedPaths $actualPaths).Count -ne 0) { throw 'Manifest does not cover the exact ZIP contents.' }
    foreach ($file in $manifest.files) {
        $path = [IO.Path]::GetFullPath((Join-Path $bundleRoot $file.path))
        if (-not $path.StartsWith($bundleRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe bundle path.' }
        if ((Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() -cne $file.sha256) { throw "Payload hash mismatch: $($file.path)" }
    }
    foreach ($required in @('Install.cmd','Uninstall.cmd','QUICKSTART.txt','README.md',
        'Installer/Install-Package.ps1','Installer/Install-PanelCladdingEditor.ps1','Installer/Uninstall-PanelCladdingEditor.ps1',
        'Plugin/PanelCladdingEditor.rhp','Plugin/PanelCladdingEditor.deps.json',
        'Plugin/libSkiaSharp.dll','Plugin/libHarfBuzzSharp.dll',
        'Plugin/runtimes/win-x64/native/libSkiaSharp.dll','Plugin/runtimes/win-x64/native/libHarfBuzzSharp.dll')) {
        if ($required -notin $expectedPaths) { throw "Required payload absent: $required" }
    }
    if ($expectedPaths -match '(RhinoCommon|Rhino.UI|Eto|Eto.Wpf|PanelCladdingEditor)\.dll$|MCP_Rhino|ModelContextProtocol|\.pdb$|runtimes/(?!win-x64/)') {
        throw 'Forbidden dependency, duplicate DLL or non-Windows runtime found.'
    }
    & (Join-Path $repo 'Project_Test/260805_TEST_rhino-plugin-assembly-identity/Verify-PluginAssemblyIdentity.ps1') -PluginPath (Join-Path $bundleRoot 'Plugin/PanelCladdingEditor.rhp')
    Write-Output "PASS ZIP checksum and all $($manifest.files.Count) payload hashes; Windows x64 dependencies; compiled identities"
    & "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-UserInstallation.ps1') -BundleRoot $bundleRoot *> (Join-Path $PSScriptRoot 'user-installation.log')
    if ($LASTEXITCODE -ne 0) { throw 'Extracted ZIP non-admin lifecycle test failed; see user-installation.log.' }
    Write-Output 'PASS extracted ZIP install/repair/validate/uninstall in Windows PowerShell 5.1'
}
finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $allowed = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts')).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe extraction cleanup.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
