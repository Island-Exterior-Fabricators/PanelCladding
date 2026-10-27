# Panel cladding Yak distribution EXET

- PLAN: [261027_PLAN_panel-cladding-yak-distribution](../Project_Plan/261027_PLAN_panel-cladding-yak-distribution.md)
- TEST: [261027_TEST_panel-cladding-yak-distribution](../Project_Test/261027_TEST_panel-cladding-yak-distribution/README.md)
- Executed: 2026-10-27, branch `codex/panel-cladding-yak-distribution`
- Version: 1.2.4 (no plug-in code change; packaging and documentation only)

## Changes

- New `Packaging/PanelCladdingEditor/Build-PanelCladdingEditorYak.ps1`: reuses the zip bundle
  build, stages its `Plugin` files with a Yak `manifest.yml`, runs `Yak.exe build --platform win`,
  checks the entries, writes `.yak.sha256`, and publishes with `-Source` (or `-PackagePath` +
  `-Source` for an already built package).
- Package README: new "Package Manager (Yak) installation" section (internal source, publishing,
  per-user migration and update); the registry section notes the uninstall-first rule.
- `QUICKSTART.txt`, root `README.md`, architecture guide and `AGENTS.md` describe the two
  mutually exclusive install channels.

## Deviations from the PLAN

- The PLAN had `-Source` always run `Yak.exe push`. Yak 0.15.2 `push` asks for a Rhino Accounts
  token even for a folder source ("There was an error retrieving your cached token. Please
  `yak login` again."). A folder source is read directly by Rhino, so the script copies the
  `.yak` into it; `Yak.exe push` is kept for `http(s)` private servers. Copying refuses to
  replace an existing version with different content.
- Added `-PackagePath` so a verified package can be published without rebuilding.

## Discovered issues

- The Yak distribution tag is `rh8_33` because the project compiles against the installed Rhino
  8.33 RhinoCommon; Package Manager offers this package only to Rhino 8.33 or newer, while the
  zip QUICKSTART recommends 8.20+. Listed as a PLAN future extension.
- The zip installer moves a `packages\8.0\PanelCladdingEditor` folder aside as a legacy copy, so
  running `Install.cmd` on a Yak machine removes the Yak install; documented as "one channel per
  machine".

## Commands and results

| Command | Result |
| --- | --- |
| `Build-PanelCladdingEditorYak.ps1` | `panelcladdingeditor-1.2.4-rh8_33-win.yak`, SHA-256 `611a63786bd49d204e676eaff7211c059a97f0c7bfbdc79692f375eace93aeb5` (25 entries) |
| `Verify-YakPackage.ps1` (first run, `yak push` to folder) | 3 FAIL: push needs `yak login` |
| `Yak.exe search --source <folder with copied .yak>` | `PanelCladdingEditor (1.2.4)`, keyword `cladding` also found |
| `Verify-YakPackage.ps1` (after copy publishing) | 29 PASS, 0 FAIL |
| `dotnet build PanelCladding.sln -c Debug -m:1` / `-c Release` | 0 warnings, 0 errors |
| `Verify-PluginAssemblyIdentity.ps1 -Configuration Debug` / `Release` | `[OK]` pluginId `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` |
| `dotnet run` `261027_TEST_panel-cladding-ud-keys` smoke | PASS |

## Acceptance alignment

Build, contents, manifest, GUID, folder-source publishing and public-server refusal are met.
Live Package Manager install/update in Rhino was not run (not authorized: it requires
uninstalling the registry installation on this machine); it is the user-run step in the TEST
README. Nothing was published to a company share or the public server.

## Rollback

Revert this branch. Users already on Yak: uninstall the package in `_PackageManager` (or
`Yak.exe uninstall PanelCladdingEditor`), then run the zip `Install.cmd`.

## Remaining limitations

- Minimum Rhino version for the Yak package follows the build machine's RhinoCommon.
- No company source path is configured yet; the user names it at publish time.

## Conclusion

The plug-in can be distributed and updated through Rhino Package Manager from a company-internal
source with one script; the registry installer remains as the alternative channel.
