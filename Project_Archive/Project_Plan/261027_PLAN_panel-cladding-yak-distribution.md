# Panel cladding Yak distribution PLAN

## Background and authorization

On 2026-10-27 the user asked whether the plug-in could update itself from GitHub from a Rhino
command. Two options were compared: a custom self-update command that downloads the release
zip and hands it to the registry installer, or Rhino's own Yak package manager. The user chose
Yak ("改用yak").

Until now the plug-in has been distributed as a zip whose PowerShell installer copies files to
`%LOCALAPPDATA%\PanelCladdingEditor\plugin\<version>` and writes a registry-only HKCU
registration. Rhino's Package Manager discovery was deliberately avoided, and the installer
moves any `packages\8.0\PanelCladdingEditor` folder aside as a legacy duplicate.

The request authorizes the PLAN -> EXET -> TEST records, the packaging scripts, documentation,
Debug/Release builds, managed regressions and building a `.yak` package locally. It does not
authorize pushing to the public Yak server, writing to a company share, uninstalling the
current registry installation on this machine, installing the Yak package into Rhino, or UI
automation; those stay with the user.

## Goals

1. One script builds a Yak package (`.yak`) of the same verified files the zip bundle ships.
2. The same script can publish the package to a company-internal Yak source (a network folder
   or private server) the user names; it refuses the public McNeel server.
3. Colleagues install and update the plug-in from Rhino's `_PackageManager` with that source
   added; Rhino performs the update on restart, so no custom self-update command is needed.
4. A documented one-time migration removes the registry installation first, so the plug-in GUID
   is never discovered twice.

## Architecture ownership

`Packaging/PanelCladdingEditor/` owns building and publishing. No plug-in code changes: the
`.rhp`, product name, commands and GUID `7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35` are unchanged.

## Design

- `Build-PanelCladdingEditorYak.ps1 [-Version] [-OutputRoot] [-Source] [-YakPath]`:
  1. Runs `Build-PanelCladdingEditorPackage.ps1` (publish, direct RHP rebuild, PE GUID check,
     dependency filtering, forbidden-dependency check) and takes its `Plugin` folder.
  2. Stages `<OutputRoot>\yak\PanelCladdingEditor-<version>` with those files and a Yak
     `manifest.yml` (name, version, authors, description, url, keywords including
     `guid:<plug-in id>` so Rhino can offer the package when a model needs the plug-in).
  3. Runs Rhino 8's `Yak.exe build --platform win` there, moves the `.yak` to `OutputRoot`,
     checks its entries and writes a `.yak.sha256`.
  4. With `-Source`, runs `Yak.exe push --source <Source>`. An `https://yak.rhino3d.com` source
     is refused so a package is never published publicly by accident.
- Yak installs under `%APPDATA%\McNeel\Rhinoceros\packages\8.0\PanelCladdingEditor\<version>`
  and Rhino loads it from there; the registry installer is kept as the legacy channel and for
  its `Uninstall.cmd`, which is the migration step.
- Documentation: package README (Yak section, internal source, migration), QUICKSTART,
  architecture guide and AGENTS.md install rules distinguish the two channels.

## Affected files

New `Packaging/PanelCladdingEditor/Build-PanelCladdingEditorYak.ps1`; package `README.md`,
`QUICKSTART.txt`; `Project_Guides/PanelCladding Architecture.md`; `AGENTS.md`; new TEST folder
with `Verify-YakPackage.ps1`.

## Usage

```powershell
# Build only
powershell -NoProfile -ExecutionPolicy Bypass -File Packaging\PanelCladdingEditor\Build-PanelCladdingEditorYak.ps1
# Build and publish to the company source
powershell -NoProfile -ExecutionPolicy Bypass -File Packaging\PanelCladdingEditor\Build-PanelCladdingEditorYak.ps1 -Source \\server\share\RhinoPackages
```

Each user: close Rhino, run the zip's `Uninstall.cmd` once, then in Rhino add the source under
Options > Package Manager > Sources and install/update `PanelCladdingEditor` from
`_PackageManager`.

## Acceptance

- Debug and Release builds pass; the relevant managed regressions pass.
- The `.yak` contains `manifest.yml` with the right name, version and GUID keyword, the
  `PanelCladdingEditor.rhp` whose PE GUID matches the manifest, the `.deps.json`, the runtime
  dependencies and Windows x64 natives, and no RhinoCommon/Rhino.UI/Eto, `.dll` copy of the
  plug-in, `.pdb` or MCP dependency.
- Publishing to a local folder source places the package where `Yak.exe` (and so Rhino) can
  find it; a public-server source is refused.
- Live install from Package Manager is a separate, user-run acceptance step.

## Rollback

Remove the Yak package from Package Manager (or `Yak.exe uninstall PanelCladdingEditor`), then
reinstall the zip with `Install.cmd`. The registry installer is unchanged.

## Future extensions

- Build against the RhinoCommon NuGet package for a lower Rhino 8 minimum (the Yak
  distribution tag follows the referenced RhinoCommon version).
- Publish from CI on a GitHub release tag.
- Retire the registry installer once every user is on Yak.
