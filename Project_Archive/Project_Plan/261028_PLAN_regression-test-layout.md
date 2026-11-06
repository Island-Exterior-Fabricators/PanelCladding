# Regression test layout PLAN

## Background and authorization

Every `Project_Test/YYMMDD_TEST_<name>/` folder holds two different things: the evidence of one
change (README, RESULTS, screenshots, summaries) and a managed smoke project that later changes
keep editing as part of the regression suite. On 2026-10-28 this blocked archiving: 33 of the
completed older trios still carry smoke projects the current suite runs. The review found:

- Later changes edit old dated folders (1.2.5 alone changed about ten August `Program.cs` files),
  so a folder's code no longer matches the evidence recorded beside it.
- Folders depend on each other: 14 projects link a fixture inside
  `261027_TEST_panel-cladding-logic-authority`, `260818_..._cell-topology` writes images into
  `261006_TEST_pc-editor-owner-boundaries`, five `Verify-Regression.ps1` scripts run other
  folders' projects by path, and about 17 programs write evidence into
  `Project_Test/<own folder>` or locate the repository by output depth.
- Running the suite rewrites tracked evidence: the 2026-10-28 Debug baseline modified 36
  committed PNGs and created two new ones.
- There is no single command for the full suite (79 projects run one by one by hand);
  `Verify-Release.ps1` runs six.
- Five smokes have failed for weeks against intentional later changes and were accepted as a
  baseline, which hides real regressions.
- Each project copies its own build output; `Project_Test` holds about 16 GB of `bin`/`obj`.

The user asked to move regression code to a fixed directory, keep TEST folders as evidence only,
and archive afterwards ("根据你的建议改动，回归代码放到固定目录，test文件夹只放证据。改动完后再统一进行归档处理").
That authorizes this PLAN -> EXET -> TEST, the moves, test-code path changes, fixes to the stale
smokes, a runner script and documentation. It does not change plug-in behaviour or version
1.2.5, and does not authorize UI automation.

## Goals

1. All rerunnable test code lives under `tests/`; nothing under `Project_Test/` is built or run
   by the regression suite.
2. One command builds and runs every managed smoke in Debug and Release and reports a result.
3. Running the suite never writes to tracked files.
4. The suite has no accepted failures: the five stale smokes are updated to current behaviour.
5. TEST folders keep their evidence and say where their code went.

## Architecture ownership

`tests/` owns regression code, fixtures, the runner and package checks. `Project_Test/` owns
dated evidence. `Packaging/PanelCladdingEditor/` keeps the build and installer scripts and calls
the identity check from its new path. `src/` is unchanged.

## Design

Layout:

```
tests/
  README.md                 layout, commands, how to add a smoke, old-folder map
  Run-Regression.ps1        one command for the managed suite
  Shared/                   CladdingLogicFixture.cs, RegressionPaths.cs
  Smoke/<name>/             the 77 managed smoke projects with their fixtures
  Smoke/Directory.Build.props
  Package/                  package, installer, registration and identity checks
  Checks/                   static checks that need no build
  Probes/                   manual probes that need arguments
```

- `<name>` is the TEST folder name without its date prefix (all names are unique), so every
  project traces back to the change that introduced it. File names do not change.
- `Smoke/Directory.Build.props` sends all smoke builds to one output folder,
  `artifacts/tests/bin/<Configuration>/`, with per-project `obj` under `artifacts/tests/obj/`,
  and compiles `Shared/RegressionPaths.cs` into every smoke. Shared output keeps one copy of the
  common dependencies instead of 77. Probes keep their own output because they target `net8.0`.
- `RegressionPaths.RepositoryRoot` walks up to `AGENTS.md`; `RegressionPaths.Output(name)`
  returns `artifacts/tests/<name>/`. Programs that wrote into `Project_Test/...` or derived the
  root from output depth use these instead.
- `Run-Regression.ps1 [-Configuration Debug,Release] [-Name <wildcards>] [-List] [-NoBuild]`
  generates a solution of the selected smokes under `artifacts/tests/`, builds it serially
  (`-m:1`, as required for the test-host build of `src`), runs each executable from the
  repository root with stdin closed and a timeout, writes a log per smoke and a JSON summary,
  and exits non-zero on any failure.
- Package checks keep their names; logs they wrote beside themselves go to
  `artifacts/tests/package/`. The packaging script and package checks call
  `tests/Package/Verify-PluginAssemblyIdentity.ps1`.
- Classification of the remaining scripts:
  - Moves to `tests/`: everything that checks the current tree or a built bundle (smokes and
    their fixtures, `Run-NativeHost.ps1`, package/installer/registration/identity/Yak checks,
    the viewport-highlight source check, the standalone/identity migration check).
  - Removed: the five `Verify-Regression.ps1` scripts, replaced by `Run-Regression.ps1 -Name`.
  - Stays as evidence: records of a one-time event that cannot be rerun meaningfully (host
    activation checks pinned to 1.0.7x-1.0.9x logs, the 260903 memo check of specific records,
    the ABC material extraction helper, the Rhino-session viewport probe, the historical
    MCP-hosted type-editor smoke).
- Stale smokes, updated to the behaviour later changes introduced on purpose:
  - `panel-cladding-hide-smoke-command`: drop its ten-command list; `panel-cladding-pc-commands`
    already owns the exact 13-command set. Keep the retirement checks.
  - `panel-cladding-type-code-format`: drop the editor `-PENDING` preview check removed with type
    generation (260930); keep the signature-format checks and the `-CL-` absence check.
  - `pc-curve-display-colors`: drop the pin to package version 1.0.73.
  - `panel-cladding-curve-release`: child CIDs join with `_` since 1.0.99 (261023); use
    `PanelCladdingCidService.ComponentSeparator`.
  - `panel-cladding-surface-release`: diagnose which write appears; update the fixture or
    expectation only if the write is an intended later behaviour, otherwise stop and report.
- Each TEST README whose code moved gets a short note naming the new location and the runner
  command; historical commands in it are left as they were.
- `PanelCladding.sln`, `README.md`, the architecture guide, the plan log and `AGENTS.md` point
  to `tests/` and state that TEST folders hold evidence only.

## Affected files

New `tests/**`; moved code from 88 `Project_Test` folders; `PanelCladding.sln`;
`Packaging/PanelCladdingEditor/Build-PanelCladdingEditorPackage.ps1`; `README.md`;
`Project_Guides/PanelCladding Architecture.md`; `Project_Guides/PanelCladding Plan Log.md`;
`AGENTS.md`; notes in moved-from TEST READMEs; new TEST folder.

## Usage

```powershell
.\tests\Run-Regression.ps1                          # Debug and Release, all smokes
.\tests\Run-Regression.ps1 -Configuration Debug -Name panel-cladding-pid*
.\tests\Package\Test-Package.ps1 -Archive <zip>
```

## Acceptance

- `tests/Run-Regression.ps1` passes 77/77 in Debug and in Release.
- After a full run, `git status` shows no change outside the intended edits.
- The 72 smokes that passed in the baseline still pass; the 5 stale ones pass after the update.
- Debug and Release solution builds pass; the package build and `Test-Package.ps1` pass on
  1.2.5 from the new paths; the identity check passes.
- No `*.cs`, `*.csproj`, `*.ps1` or `*.py` under `Project_Test` is referenced by the solution,
  packaging or `tests/`.
- No broken relative Markdown links.

## Rollback

Revert the branch. Git records the moves as renames, so history of each file follows it.

## Future extensions

- Convert the console smokes to one test project with a test framework.
- Run `Run-Regression.ps1` in CI.
