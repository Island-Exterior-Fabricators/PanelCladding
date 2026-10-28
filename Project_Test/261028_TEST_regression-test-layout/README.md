# Regression test layout TEST

Evidence for [the PLAN](../../Project_Plan/261028_PLAN_regression-test-layout.md) and
[the EXET](../../Project_Exet/261028_EXET_regression-test-layout.md). The code it verifies is in
[`tests/`](../../tests/README.md).

| File | Contents |
| --- | --- |
| `folder-map.md` | Where each older TEST folder's code moved, and the scripts that stayed as evidence. |
| `baseline-Debug-before-move.json` | Debug run of all 79 projects from `Project_Test` before the move: 72 pass. |
| `regression-Debug.json`, `regression-Release.json` | `tests/Run-Regression.ps1` after the move and the smoke updates: 77/77 each. |

## Commands

```powershell
.\tests\Run-Regression.ps1                                   # Debug and Release, 77 smokes
dotnet build .\PanelCladding.sln -c Debug -m:1
dotnet build .\PanelCladding.sln -c Release -m:1
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -Version 1.2.5
.\tests\Package\Test-Package.ps1 -Archive .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.2.5-win-x64.zip
.\tests\Checks\Test-ViewportHighlightDrawing.ps1
python -I .\tests\Checks\Verify-Migration.py
git status --short                                           # after a run: no tracked file changed
```

## Before the move

The baseline ran each project with `dotnet run` from the repository root, stdin closed. Failures:

| Project | Exit | Cause |
| --- | --- | --- |
| `PluginAssemblyIdentityProbe` | 2 | Probe needs RHP paths as arguments; run by `Verify-PluginAssemblyIdentity.ps1`. |
| `PanelExtrusionNativeRuntimeProbe` | unhandled | Probe needs a bundle path and a PDF. |
| `PanelCladdingHideSmokeCommandSmoke` | 1 | Expected the ten commands of August; 13 exist. |
| `PanelCladdingTypeCodeFormatSmoke` | unhandled | Expected the editor `-PENDING` preview removed with type generation (260930). |
| `PCCurveDisplayColorsSmoke` | unhandled | Required package version 1.0.73. |
| `PanelCladdingCurveReleaseSmoke` | unhandled | Expected `-` before child CID parts; `_` since 1.0.99 (261023). |
| `PanelCladdingSurfaceReleaseSmoke` | unhandled | Fixture objects had no names; sync now renames to the CID short name (261006/261023). |

The run modified 36 committed PNGs in older TEST folders and created two untracked ones in
`260818_TEST_material-catalogue-editing`; they were restored with `git checkout -- Project_Test`
and the two new files deleted.

## After the move

- First Debug run from `tests/` with only path changes: 72/77, the same five stale failures
  with the same messages. Probes are no longer part of the suite.
- After updating the five smokes: Debug 77/77 (115 s), Release 77/77 (119 s). `git status`
  showed no change to any tracked file outside the edits.
- Solution builds: Debug and Release exit 0.
- Package 1.2.5 rebuilt from the new identity path: GUID check `[OK]`, RHP SHA-256
  `3E0A7C41E6B6078FE4803AE60D17F55EB38DE8ADAC4FB31E566A81C55358D4E7`, same 32 bundle hashes as the
  package staged earlier the same day; `Test-Package.ps1` from `tests/Package` passed (checksum,
  payload hashes, identity, isolated install/repair/validate/uninstall).
- `Test-ViewportHighlightDrawing.ps1` `[OK]`; `Verify-Migration.py` `PASS`.
- Disk: `Project_Test` 16 GB of `bin`/`obj` removed (4.9 MB left); shared smoke output
  `artifacts/tests` 163 MB after both configurations.

Not run: `Verify-YakPackage.ps1` (needs a Yak build), `Verify-Release.ps1` end to end,
`Test-PanelCladdingPluginRegistration.ps1` and `PanelRegistryOnlyInstallationSmoke.ps1`; their
changes are path-only. Smokes' Rhino-native parts still print `[SKIP]`.
