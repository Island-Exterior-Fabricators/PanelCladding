[CmdletBinding()]
param(
    [string] $Bundle
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

function Assert-That([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}

function Read-Text([string] $Path) { [System.IO.File]::ReadAllText($Path) }

$manifestPath = Join-Path $repo 'Packaging\PanelCladdingEditor\package-manifest.json'
$manifest = Read-Text $manifestPath | ConvertFrom-Json
$pluginId = ([string] $manifest.pluginId).ToLowerInvariant()
Assert-That ($pluginId -match '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$') "Packaging/PanelCladdingEditor/package-manifest.json: pluginId '$($manifest.pluginId)' is not a GUID."
Assert-That ($pluginId -ne '00000000-0000-0000-0000-000000000000') 'Packaging/PanelCladdingEditor/package-manifest.json: pluginId is the empty GUID.'
foreach ($file in 'src/PanelCladdingEditor/AssemblyInfo.cs', 'src/PanelCladdingEditor/PanelCladdingEditorPlugin.cs') {
    Assert-That ((Read-Text (Join-Path $repo $file)).ToLowerInvariant().Contains($pluginId)) "${file}: does not declare the manifest plug-in GUID $pluginId."
}

$references = 0
$projects = @(& git -C $repo ls-files -- '*.csproj')
if ($LASTEXITCODE -ne 0) { throw 'git ls-files failed.' }
foreach ($project in $projects) {
    $projectPath = Join-Path $repo $project
    [xml] $xml = Read-Text $projectPath
    foreach ($element in @($xml.SelectNodes('//ProjectReference'))) {
        $include = $element.GetAttribute('Include')
        $target = [System.IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $projectPath) $include))
        Assert-That ($target.StartsWith($repo + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) "${project}: project reference '$include' points outside the repository."
        Assert-That (Test-Path -LiteralPath $target -PathType Leaf) "${project}: project reference '$include' does not exist."
        Assert-That ($include -notmatch 'MCP_Rhino') "${project}: project reference '$include' names MCP_Rhino."
        $references++
    }
    Assert-That ((Read-Text $projectPath) -notmatch 'MCP_Rhino') "${project}: names MCP_Rhino."
}

$solution = Read-Text (Join-Path $repo 'PanelCladding.sln')
Assert-That ($solution -notmatch 'MCP_Rhino') 'PanelCladding.sln: names MCP_Rhino.'
foreach ($match in [regex]::Matches($solution, '= "[^"]+", "([^"]+\.csproj)"')) {
    $path = $match.Groups[1].Value
    Assert-That (Test-Path -LiteralPath (Join-Path $repo $path) -PathType Leaf) "PanelCladding.sln: project '$path' does not exist."
}

$hashes = 0
if ($Bundle) {
    $bundleRoot = (Resolve-Path -LiteralPath $Bundle).Path
    $metadata = Read-Text (Join-Path $bundleRoot 'bundle-manifest.json') | ConvertFrom-Json
    Assert-That ($metadata.name -eq $manifest.name -and $metadata.version -eq $manifest.version) 'bundle-manifest.json: name or version differs from the package manifest.'
    foreach ($entry in @($metadata.files)) {
        $path = [System.IO.Path]::GetFullPath((Join-Path $bundleRoot ([string] $entry.path)))
        Assert-That ($path.StartsWith($bundleRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) "bundle-manifest.json: '$($entry.path)' points outside the bundle."
        Assert-That (Test-Path -LiteralPath $path -PathType Leaf) "bundle-manifest.json: '$($entry.path)' is missing."
        $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        Assert-That ($actual -eq ([string] $entry.sha256).ToLowerInvariant()) "Hash mismatch: $($entry.path)"
        $hashes++
    }
    $names = @(Get-ChildItem -LiteralPath (Join-Path $bundleRoot 'Plugin') -Recurse -File | ForEach-Object { $_.Name })
    Assert-That ($names -contains 'PanelCladdingEditor.rhp') 'Plugin/ has no PanelCladdingEditor.rhp.'
    Assert-That ($names -notcontains 'PanelCladdingEditor.dll') 'Plugin/ holds a PanelCladdingEditor.dll twin.'
    Assert-That (@($names | Where-Object { $_ -like 'MCP_Rhino*' -or $_ -like 'ModelContextProtocol*' }).Count -eq 0) 'Plugin/ holds an MCP dependency.'
}

[pscustomobject]@{ result = 'PASS'; projectReferences = $references; pluginId = $pluginId; bundleHashes = $hashes } | ConvertTo-Json -Compress
