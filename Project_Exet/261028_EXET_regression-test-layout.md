# Regression test layout EXET

## Corresponding plan and execution date

[PLAN](../Project_Plan/261028_PLAN_regression-test-layout.md),
[TEST evidence](../Project_Test/261028_TEST_regression-test-layout/README.md).
Executed 2026-10-28 on branch `codex/regression-test-layout`, host NOVAN-HOME. Plug-in code and
version 1.2.5 are unchanged.

## Changes

- `tests/` created: `Smoke/<name>/` (77 managed smokes with their fixtures, named after their
  original TEST folder without the date), `Shared/` (`CladdingLogicFixture.cs`, new
  `RegressionPaths.cs`), `Package/` (package, installer, registration, identity and Yak checks
  with the identity probe), `Checks/` (viewport-highlight source check, standalone/identity check),
  `Probes/panel-extrusion-native-runtime`, `README.md` and `Run-Regression.ps1`.
- Code moved from 88 `Project_Test` folders with `git mv` (176 renames). Five
  `Verify-Regression.ps1` scripts were removed; `Run-Regression.ps1 -Name` replaces them. Nine
  scripts that record one-time events stayed as evidence (listed in the TEST `folder-map.md`).
- Every moved-from TEST README starts with a note naming the new location and the equivalent
  command; historical text is unchanged. One EXET link to a moved `Program.cs` now points to
  `tests/`.
- `Smoke/Directory.Build.props` sends smoke builds to `artifacts/tests/bin/<Configuration>/` and
  compiles `RegressionPaths.cs` into each smoke. Programs that wrote renders into
  `Project_Test/...`, read `src` relative to the working directory, or located the repository
  from output depth now use `RegressionPaths.Output(name)` / `RegressionPaths.RepositoryRoot`.
  The `cell-topology` renders that fed `261006_TEST_pc-editor-owner-boundaries` go to
  `artifacts/tests/pc-editor-owner-boundaries/`.
- Project references gained one `..`; 14 links to the logic fixture and the `tutorial-fixture.json`
  link point into `tests/`; `Run-NativeHost.ps1` uses the shared output.
- `Run-Regression.ps1`: `-Configuration` (Debug, Release by default), `-Name` wildcards, `-List`,
  `-NoBuild`, `-TimeoutSeconds`. It builds each smoke serially, runs the executable from the
  repository root with stdin closed, logs to `artifacts/tests/logs/<Configuration>/`, writes
  `artifacts/tests/regression-<Configuration>.json` and exits 1 on any failure.
- Package checks log to `artifacts/tests/package/` and call the identity check beside them;
  `Verify-Release.ps1` runs its six release smokes through `Run-Regression.ps1`.
  `Build-PanelCladdingEditorPackage.ps1` calls `tests\Package\Verify-PluginAssemblyIdentity.ps1`.
- `PanelCladding.sln` lists the standalone smoke under a `tests` folder.
- Stale smokes updated to the behaviour later changes introduced on purpose:
  - `panel-cladding-hide-smoke-command`: dropped its ten-command list; the
    `panel-cladding-pc-commands` smoke owns the exact 13-command set.
  - `panel-cladding-type-code-format`: dropped the editor `-PENDING` preview check (removed with
    type generation, 260930); kept the format checks and the `-CL-` absence check.
  - `pc-curve-display-colors`: dropped the pin to package version 1.0.73.
  - `panel-cladding-curve-release`: child CIDs use `_` (1.0.99); the existing-curve fixture
    carries its current CID and name.
  - `panel-cladding-surface-release`: the fixture panel and surfaces carry their CID-derived
    names.
- `README.md`, `Project_Guides/PanelCladding Architecture.md`, `Project_Guides/PanelCladding Plan
  Log.md` and `AGENTS.md` describe `tests/`, evidence-only TEST folders, updating affected smokes,
  and the archive rule (adapted from the Wireframe Web repository's rule 4, with this
  repository's manifest practice).

## Deviations from plan

- The PLAN put each smoke's `obj` under `artifacts/tests/obj/`. Only `bin` is shared; `obj` stays
  in each project folder (ignored). Relocating `obj` per project is a known source of restore
  failures for WPF's temporary markup-compile project, so it was not tried; the size reduction
  comes from the shared `bin`.
- The PLAN expected 63 moved-from folders; the dry run found 88 (smokes plus package and check
  scripts). Corrected in the PLAN before the move.
- `surface-release` diagnosis: a temporary print of the commit request showed one panel write
  caused by `NameChanged` (the fixture panel had no object name; PCSyncSrf names panels from
  their CID since 261006/261023), not by cladding or unit attributes. The fixture now names its
  objects, and every original assertion is unchanged. `curve-release` failed on the same naming
  rule after the separator fix and got the same fixture change. The print was removed.
- The package was rebuilt to verify the moved identity path. Its 32 bundle hashes equal the 1.2.5
  bundle staged for installation earlier the same day; only the zip container hash differs.

## Discovered issues

- Every regression run used to rewrite tracked evidence: the baseline modified 36 committed PNGs
  and created two new files. Runs now write only under `artifacts/`.
- `Project_Test` held 16 GB of per-project build output, removed with the move; the shared smoke
  output is 163 MB for both configurations.
- The five failing smokes were stale, not product regressions; each failure traced to an
  intentional later change.

## Commands and results

See the [TEST README](../Project_Test/261028_TEST_regression-test-layout/README.md). In summary:

- Baseline (old layout, Debug): 72/79; failures were two probes needing arguments and the five
  stale smokes.
- `tests/Run-Regression.ps1 -Configuration Debug` after the move only: 72/77, the same five.
- `tests/Run-Regression.ps1` after the updates: Debug 77/77, Release 77/77; `git status` clean
  apart from the intended edits.
- `dotnet build .\PanelCladding.sln -c Debug|Release -m:1`: exit 0.
- `Build-PanelCladdingEditorPackage.ps1 -Version 1.2.5`: identity `[OK]`, RHP SHA-256
  `3E0A7C41E6B6078FE4803AE60D17F55EB38DE8ADAC4FB31E566A81C55358D4E7`.
- `tests\Package\Test-Package.ps1`: PASS (checksum, 32 payload hashes, identity, isolated
  install/repair/validate/uninstall).
- `tests\Checks\Test-ViewportHighlightDrawing.ps1` `[OK]`; `tests\Checks\Verify-Migration.py` PASS.
- Relative Markdown links: 0 broken.

## Acceptance alignment

All PLAN acceptance items are met, except that the package checks not run (below) were verified
by reading their path-only changes.

## Rollback

Revert the branch. The moves are renames, so file history follows each file.

## Remaining limitations

- Not run: `Verify-YakPackage.ps1` (needs a Yak build), `Verify-Release.ps1` end to end,
  `Test-PanelCladdingPluginRegistration.ps1`, `PanelRegistryOnlyInstallationSmoke.ps1`.
- Smokes stay console programs; Rhino-native parts still print `[SKIP]` and need live acceptance.
- Historical EXETs and TEST READMEs keep their original `Project_Test/...` commands.

## Conclusion

Regression code lives in `tests/` and runs with one command; TEST folders hold evidence only.
The full suite passes in Debug and Release with no accepted failures, runs leave tracked files
untouched, and packaging works from the new paths.
