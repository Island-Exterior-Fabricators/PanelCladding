# PanelCladding

Standalone Rhino 8 panel cladding editor, migrated from
[MCP_Rhino](https://github.com/novan-xu/MCP_Rhino) on 2026-10-13.
The plug-in/assembly name remains **PanelCladdingEditor** and its primary command
is **PCEditor**. No MCP server or Router is needed.

## Build

Requires Windows, the .NET 8 SDK, and Rhino 8 at `C:\Program Files\Rhino 8`.
For another Rhino installation path, update RhinoCommon HintPath in the project.

```powershell
dotnet build .\PanelCladding.sln -c Debug -m:1
dotnet build .\PanelCladding.sln -c Release -m:1
.\Project_Test\261007_TEST_panel-cladding-pid-planar-bounds\Verify-Regression.ps1
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1
```

Build the production project after running DLL-host smoke tests when using its RHP
directly. Packaging does this automatically. Packages are generated under
`Packaging/PanelCladdingEditor/artifacts/` and are not tracked in Git.

## Use and installation

See [commands, workflows and installation](Packaging/PanelCladdingEditor/README.md).
Installation remains registry-only under
`%LOCALAPPDATA%\PanelCladdingEditor\plugin\<version>`. Moving this source checkout
does not change the installed plug-in or its registration. Follow the independent
host attestation requirements in [AGENTS.md](AGENTS.md) before activation.

## Repository

- `src/PanelCladdingEditor/`: application, Rhino adapters and WPF UI.
- `Packaging/PanelCladdingEditor/`: release build and installer scripts.
- `Design/PanelCladdingEditor/`: design reference.
- `Project_Plan/`, `Project_Exet/`, `Project_Test/`: development records and tests.

The extraction retains 25 relevant commits from original source commit
`f6dc9d095c28dfc72ebcfd8a4ba7c06b336dcd71`; filtered commit IDs necessarily differ.
See the [migration inventory](Project_Test/261013_TEST_panel-cladding-repository-migration/migration-inventory.json)
and [commit map](Project_Test/261013_TEST_panel-cladding-repository-migration/commit-map.txt).
Historical reports retain their original paths and GitHub links as provenance.
The original MCP-hosted type-editor smoke is historical; use the standalone smoke
project and later regression projects for current validation.
