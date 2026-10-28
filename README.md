# PanelCladding

Standalone Rhino 8 panel cladding editor, migrated from
[MCP_Rhino](https://github.com/novan-xu/MCP_Rhino) on 2026-10-13.
The plug-in/assembly name remains **PanelCladdingEditor** and its primary command
is **PCEditor**. No MCP server or Router is needed.

## Install (no administrator access)

Download **PanelCladdingEditor-1.2.5-win-x64.zip** from the
[latest release](https://github.com/novan-xu/PanelCladding/releases/latest).
Extract all files, close Rhino, and double-click **Install.cmd** as your normal
Windows user. After installation and validation pass, start Rhino and run **PCEditor**.
To upgrade an earlier zip installation, do the same with the new package; it replaces the
previous version. The [release notes](Packaging/PanelCladdingEditor/RELEASE-NOTES-1.2.5.md)
list the attribute and naming changes since 1.0.97.

Requires Windows x64 and Rhino 8 running .NET 8. You do not need the .NET SDK or
an MCP server to install. Files live in your LocalAppData folder and registration
uses only `HKEY_CURRENT_USER`. The launcher does not request elevation or change
your saved PowerShell execution policy. Organization policies may restrict unsigned
scripts. Close Rhino and use **Uninstall.cmd** to remove the package.

The release includes a SHA-256 checksum; repair and validation instructions are
in the bundled **QUICKSTART.txt**. Do not install a second copy via Package Manager.

Alternatively, when the plug-in is published to a company-internal Package Manager
source, install and update it from Rhino's **_PackageManager** instead (uninstall the
zip first). See
[Package Manager (Yak) installation](Packaging/PanelCladdingEditor/README.md#package-manager-yak-installation).

## Build

Requires Windows, the .NET 8 SDK, and Rhino 8 at `C:\Program Files\Rhino 8`.
For another Rhino installation path, update RhinoCommon HintPath in the project.

```powershell
dotnet build .\PanelCladding.sln -c Debug -m:1
dotnet build .\PanelCladding.sln -c Release -m:1
.\tests\Run-Regression.ps1
.\tests\Package\Verify-PluginAssemblyIdentity.ps1 -Configuration Release
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorYak.ps1
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
- `tests/`: regression smokes, package checks and the [test runner](tests/README.md).
- `Project_Plan/`, `Project_Exet/`, `Project_Test/`: development records; `Project_Test/` holds
  evidence only.

The extraction retains 25 relevant commits from original source commit
`f6dc9d095c28dfc72ebcfd8a4ba7c06b336dcd71`; filtered commit IDs necessarily differ.
See the [migration inventory](Project_Archive/Project_Test/261013_TEST_panel-cladding-repository-migration/migration-inventory.json)
and [commit map](Project_Archive/Project_Test/261013_TEST_panel-cladding-repository-migration/commit-map.txt).
Historical reports retain their original paths and GitHub links as provenance.
The original MCP-hosted type-editor smoke is historical; use `tests/` for current validation.
