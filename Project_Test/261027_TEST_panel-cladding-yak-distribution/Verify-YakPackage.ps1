[CmdletBinding()]
param(
    # Built .yak package. Defaults to the single package for the manifest version in the artifacts folder.
    [string] $PackagePath,
    [string] $YakPath = (Join-Path $env:ProgramFiles 'Rhino 8\System\Yak.exe')
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$packaging = Join-Path $repoRoot 'Packaging\PanelCladdingEditor'
$definition = Get-Content -Raw -LiteralPath (Join-Path $packaging 'package-manifest.json') | ConvertFrom-Json
$version = [string]$definition.version
if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $candidates = @(Get-ChildItem -LiteralPath (Join-Path $packaging 'artifacts') -Filter "panelcladdingeditor-$version-*-win.yak" -File)
    if ($candidates.Count -ne 1) { throw "Expected one $version .yak package, found $($candidates.Count). Pass -PackagePath." }
    $PackagePath = $candidates[0].FullName
}
$PackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
$failures = @()
function Check([bool] $condition, [string] $message) {
    if ($condition) { Write-Host "PASS $message" } else { Write-Host "FAIL $message"; $script:failures += $message }
}

$work = Join-Path ([IO.Path]::GetTempPath()) ("pc-yak-test-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    # 1. Package contents.
    $extract = Join-Path $work 'extract'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($PackagePath, $extract)
    $entries = @(Get-ChildItem -LiteralPath $extract -Recurse -File | ForEach-Object {
        $_.FullName.Substring($extract.Length).TrimStart('\').Replace('\', '/')
    })
    Check ([IO.Path]::GetFileName($PackagePath) -match "^panelcladdingeditor-$([regex]::Escape($version))-rh8_\d+-win\.yak$") 'file name carries name, version, rh8 tag and win platform'
    foreach ($required in @('manifest.yml', 'PanelCladdingEditor.rhp', 'PanelCladdingEditor.deps.json',
            'DocumentFormat.OpenXml.dll', 'UglyToad.PdfPig.Rendering.Skia.dll', 'libSkiaSharp.dll', 'libHarfBuzzSharp.dll')) {
        Check ($entries -contains $required) "package contains $required"
    }
    foreach ($forbidden in @('RhinoCommon.dll', 'Rhino.UI.dll', 'Eto.dll', 'Eto.Wpf.dll', 'PanelCladdingEditor.dll')) {
        Check (-not ($entries | Where-Object { [IO.Path]::GetFileName($_) -eq $forbidden })) "package omits $forbidden"
    }
    Check (-not ($entries | Where-Object { $_ -like '*.pdb' })) 'package omits .pdb files'
    Check (-not ($entries | Where-Object { [IO.Path]::GetFileName($_) -match '^(MCP_Rhino|ModelContextProtocol|Microsoft\.Extensions\.Hosting)' })) 'package omits MCP dependencies'
    Check (-not ($entries | Where-Object { $_.StartsWith('runtimes/') -and -not $_.StartsWith('runtimes/win-x64/') })) 'package ships only win-x64 runtimes'

    # 2. Manifest.
    $manifest = Get-Content -Raw -LiteralPath (Join-Path $extract 'manifest.yml')
    $guid = ([string]$definition.pluginId).ToLowerInvariant()
    Check ($manifest -match '(?m)^name:\s*PanelCladdingEditor\s*$') 'manifest name is PanelCladdingEditor'
    Check ($manifest -match "(?m)^version:\s*$([regex]::Escape($version))\s*$") "manifest version is $version"
    Check ($manifest -match "(?m)^\s*-\s*guid:$guid\s*$") 'manifest keywords carry the plug-in GUID'
    Check ($manifest -match '(?m)^platform:\s*win\s*$') 'manifest platform is win'

    # 3. Compiled plug-in identity of the packaged RHP.
    $identity = Join-Path $repoRoot 'Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1'
    $identityOk = $true
    try { & $identity -PluginPath (Join-Path $extract 'PanelCladdingEditor.rhp') | Out-Host } catch { $identityOk = $false; Write-Host $_ }
    Check $identityOk 'packaged RHP assembly GUID matches the manifest and plug-in class'

    # 4. Folder source round trip: publish through the script, then search through the same source.
    $buildYak = Join-Path $packaging 'Build-PanelCladdingEditorYak.ps1'
    $source = Join-Path $work 'source'
    New-Item -ItemType Directory -Path $source | Out-Null
    $publishOk = $true
    try { & $buildYak -PackagePath $PackagePath -Source $source | Out-Null } catch { $publishOk = $false; Write-Host $_ }
    Check $publishOk 'publishing to a folder source succeeds without a Rhino Accounts login'
    $published = @(Get-ChildItem -LiteralPath $source -Recurse -File -Filter '*.yak')
    Check ($published.Count -eq 1 -and $published[0].Name -eq [IO.Path]::GetFileName($PackagePath)) 'folder source holds the published package'
    $search = (& $YakPath search --source $source --all --prerelease PanelCladdingEditor 2>&1 | Out-String)
    Write-Host $search.Trim()
    Check ($search -match "PanelCladdingEditor \($([regex]::Escape($version))\)") 'yak search through the folder source finds the version'
    $republishOk = $true
    try { & $buildYak -PackagePath $PackagePath -Source $source | Out-Null } catch { $republishOk = $false }
    Check $republishOk 'republishing identical content is a no-op'
    $tampered = Join-Path $work ([IO.Path]::GetFileName($PackagePath))
    Copy-Item -LiteralPath $PackagePath -Destination $tampered
    [IO.File]::AppendAllText($tampered, 'x')
    $conflictRefused = $false
    try { & $buildYak -PackagePath $tampered -Source $source | Out-Null } catch { $conflictRefused = $_.Exception.Message -match 'Bump the version' }
    Check $conflictRefused 'replacing a published version with different content is refused'

    # 5. The build script refuses the public server before building anything.
    $refused = $false
    try {
        & (Join-Path $packaging 'Build-PanelCladdingEditorYak.ps1') -OutputRoot (Join-Path $work 'out') -Source 'https://yak.rhino3d.com/' | Out-Null
    } catch { $refused = $_.Exception.Message -match 'public McNeel package server' }
    Check $refused 'build script refuses the public Yak server'
    Check (-not (Test-Path -LiteralPath (Join-Path $work 'out'))) 'refused publish builds nothing'
} finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) { throw "$($failures.Count) Yak package check(s) failed." }
Write-Host "All Yak package checks passed: $PackagePath"
