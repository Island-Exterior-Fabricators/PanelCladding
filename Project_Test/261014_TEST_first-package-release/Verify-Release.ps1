[CmdletBinding()]
param([string[]] $Configurations = @('Debug', 'Release'))
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$suites = @(
    '260804_TEST_standalone-panel-cladding-editor/PanelCladdingEditorSmoke.csproj',
    '261007_TEST_panel-cladding-pid/PanelCladdingPidSmoke.csproj',
    '261007_TEST_panel-cladding-pid-layer-scope/PanelCladdingPidLayerScopeSmoke.csproj',
    '261007_TEST_panel-cladding-pid-setup-keys/PanelCladdingPidSetupKeysSmoke.csproj',
    '261007_TEST_panel-cladding-point-order/PanelCladdingPointOrderSmoke.csproj',
    '261007_TEST_panel-cladding-pid-planar-bounds/PanelCladdingPidPlanarBoundsSmoke.csproj'
)
Push-Location $repo
try {
    foreach ($configuration in $Configurations) {
        foreach ($suite in $suites) {
            $name = [IO.Path]::GetFileNameWithoutExtension($suite)
            & dotnet run --project "Project_Test/$suite" -c $configuration *> (Join-Path $PSScriptRoot "$configuration-$name.log")
            if ($LASTEXITCODE -ne 0) { throw "$configuration $name failed; see log." }
            Write-Output "PASS $configuration $name"
        }
        & dotnet build PanelCladding.sln -c $configuration -t:Rebuild -m:1 --nologo *> (Join-Path $PSScriptRoot "$configuration-build.log")
        if ($LASTEXITCODE -ne 0) { throw "$configuration production build failed; see log." }
        Write-Output "PASS $configuration solution build"
        # The solution also builds the DLL-host smoke; restore the direct RHP last.
        & dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c $configuration -t:Rebuild --nologo *> (Join-Path $PSScriptRoot "$configuration-rhp-build.log")
        if ($LASTEXITCODE -ne 0) { throw "$configuration direct RHP rebuild failed; see log." }
        Write-Output "PASS $configuration production RHP build"
        & ./Project_Test/260805_TEST_rhino-plugin-assembly-identity/Verify-PluginAssemblyIdentity.ps1 -Configuration $configuration -SkipBuild
    }
    & ./Packaging/PanelCladdingEditor/Build-PanelCladdingEditorPackage.ps1
    $version = (Get-Content -Raw ./Packaging/PanelCladdingEditor/package-manifest.json | ConvertFrom-Json).version
    $bundle = Join-Path $repo "Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-$version"
    & (Join-Path $PSScriptRoot 'Test-Package.ps1') -Archive "$bundle-win-x64.zip"
    & ./Project_Test/260826_TEST_panel-cladding-plugin-registration/Test-PanelCladdingPluginRegistration.ps1 -BundleRoot $bundle
}
finally { Pop-Location }
