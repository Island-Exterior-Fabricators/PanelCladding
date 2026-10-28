# Panel cladding Yak distribution TEST

> **Code moved on 2026-10-28.** This folder keeps the evidence only: `Verify-YakPackage.ps1` moved to [`tests/Package`](../../tests/Package/). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

Regression for [the PLAN](../../Project_Plan/261027_PLAN_panel-cladding-yak-distribution.md):
the plug-in is built as a Rhino Yak package and published to a company-internal source.

## Package verification (`Verify-YakPackage.ps1`)

Build first, then verify the built package:

```powershell
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorYak.ps1
.\Project_Test\261027_TEST_panel-cladding-yak-distribution\Verify-YakPackage.ps1
```

Checks, in a temporary folder that is removed afterwards:

- the file name is `panelcladdingeditor-<version>-rh8_<n>-win.yak`;
- the package holds `manifest.yml`, the RHP, its `.deps.json`, the OpenXml/PdfPig managed
  dependencies and the top-level Skia/HarfBuzz natives, and omits RhinoCommon, Rhino.UI, Eto,
  a `.dll` copy of the plug-in, `.pdb` files, MCP dependencies and non-win-x64 runtimes;
- `manifest.yml` name, version, `guid:<plug-in id>` keyword and `win` platform;
- the packaged RHP passes `Verify-PluginAssemblyIdentity.ps1 -PluginPath` (assembly GUID equals
  the manifest and plug-in class GUID);
- publishing to a local folder source succeeds without a Rhino Accounts login, `Yak.exe search`
  through that source finds the version, republishing identical content is a no-op and
  republishing different content under the same version is refused;
- `-Source https://yak.rhino3d.com/` is refused before anything is built.

## Not covered (user-run live acceptance)

Requires Rhino and the user's machine; not run by the agent:

1. Close Rhino; run the installed zip's `Uninstall.cmd`.
2. Add a source folder holding the `.yak` in `Options > Package Manager > Sources`.
3. `_PackageManager` -> install `PanelCladdingEditor`; restart Rhino; `PCEditor` opens and
   `_PlugInManager` shows one PanelCladdingEditor loaded from
   `%APPDATA%\McNeel\Rhinoceros\packages\8.0\PanelCladdingEditor\<version>`.
4. Publish a higher version; `_PackageManager` offers it as an update; it loads after restart.
