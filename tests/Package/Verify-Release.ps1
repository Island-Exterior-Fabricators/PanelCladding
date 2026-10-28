[CmdletBinding()]
param([string[]] $Configurations = @('Debug', 'Release'))
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$logs = Join-Path $repo 'artifacts\tests\package'
New-Item -ItemType Directory -Force -Path $logs | Out-Null
$suites = @(
    'standalone-panel-cladding-editor',
    'panel-cladding-pid',
    'panel-cladding-pid-layer-scope',
    'panel-cladding-pid-setup-keys',
    'panel-cladding-point-order',
    'panel-cladding-pid-planar-bounds'
)
Push-Location $repo
try {
    foreach ($configuration in $Configurations) {
        & ./tests/Run-Regression.ps1 -Configuration $configuration -Name $suites
        if ($LASTEXITCODE -ne 0) { throw "$configuration release smokes failed; see artifacts/tests/logs." }
        & dotnet build PanelCladding.sln -c $configuration -t:Rebuild -m:1 --nologo *> (Join-Path $logs "$configuration-build.log")
        if ($LASTEXITCODE -ne 0) { throw "$configuration production build failed; see log." }
        Write-Output "PASS $configuration solution build"
        # The solution also builds the DLL-host smoke; restore the direct RHP last.
        & dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c $configuration -t:Rebuild --nologo *> (Join-Path $logs "$configuration-rhp-build.log")
        if ($LASTEXITCODE -ne 0) { throw "$configuration direct RHP rebuild failed; see log." }
        Write-Output "PASS $configuration production RHP build"
        & (Join-Path $PSScriptRoot 'Verify-PluginAssemblyIdentity.ps1') -Configuration $configuration -SkipBuild
    }
    & ./Packaging/PanelCladdingEditor/Build-PanelCladdingEditorPackage.ps1
    $version = (Get-Content -Raw ./Packaging/PanelCladdingEditor/package-manifest.json | ConvertFrom-Json).version
    $bundle = Join-Path $repo "Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-$version"
    & (Join-Path $PSScriptRoot 'Test-Package.ps1') -Archive "$bundle-win-x64.zip"
    & (Join-Path $PSScriptRoot 'Test-PanelCladdingPluginRegistration.ps1') -BundleRoot $bundle
}
finally { Pop-Location }
