# PCbake command EXET

## Corresponding plan and execution date

Executed 2026-10-31 under [the user-authorized PLAN](../Project_Plan/261031_PLAN_pcbake-command.md).

## Related artifacts

[TEST evidence](../Project_Test/261031_TEST_pcbake-command/README.md); new smoke
[`tests/Smoke/pcbake-command`](../tests/Smoke/pcbake-command/Program.cs). Branch
`codex/pcbake-command` from `codex/command-name-casing` (`0f308e1`), in the worktree
`C:\01_Projects\Panel Cladding Full\PC-casing`. Commit `4a1f007`;
[PR 11](https://github.com/nuofan-island/PanelCladding/pull/11), opened and merged into `master` at the
user's request after [PR 10](https://github.com/nuofan-island/PanelCladding/pull/10).

## Implemented scope

- Commands: `PanelCladdingUpdateCommand` became `PanelCladdingBakeCommand` (`PCbake`, same GUID
  `F2C41D75-...`); `PanelCladdingSpawnSrfCommand` and `PanelCladdingSpawnCrvCommand` were deleted.
  11 commands remain. The material-colour lookup moved to the bake command and runs only when
  surfaces are in scope.
- Option: the selection prompt carries `Scope` = `Surfaces` | `Curves` | `All` from
  `Enum.GetNames<PanelCladdingBakeScope>()`; `scopeIndex` is a local starting at 0, so every run
  starts at `Surfaces`. With a preselection the prompt is shown once (the RhinoCommon
  options-with-preselection pattern: no clear on entry, no unselect on exit), and Enter accepts. A
  preselection stays selected afterwards, as before; panels picked at the prompt are unselected.
- Domain: `PanelCladdingBakeScope`; `PanelCladdingExistingDependency.SourcePanelObjectId`;
  `PreservedCurvePanelIds` on the reconciliation plan and the update result.
- Application: `PanelCladdingDependencyReconciliationService.Includes(scope, kind)`,
  `CurvePreservedPanels(existing)` and `CreatePlan(expected, existing, scope)`. Existing objects
  outside the scope and every existing curve are dropped from the plan, so they are never rebuilt
  or deleted. Expected curves of a panel that owns any curve are dropped. Kept panels are reported
  when curves are in scope.
- Live update: `Update(filePath, ids, scope)`; existing dependencies are read before generation
  and record their single owner; surfaces are prepared only in scope and curves only in scope for
  panels without curves, so a curve-planning error cannot block a surface bake. CID normalization,
  point order, duplicate-CID skipping, ambiguous legacy dependencies and the single Undo record are
  unchanged.
- Removed dead code: `LivePanelCladdingSpawnService.Spawn`, `ILivePanelCladdingSpawnService`,
  `PanelCladdingSpawnResult` and the helpers only that entry used (`IsManagedCidConflict`,
  `RollBackCreatedObjects`, its `ResolveDocument` copy). The service stays as PCbake's per-panel
  generator.
- Output: `PCbake (<scope>) processed ...` and
  `Kept the existing curves of N panel(s); delete a panel's curves to bake new ones.`
- Installer command list and both package checks: `PCbake` added, `PCupdate`, `PCspawnSrf`,
  `PCspawnCrv` removed. Package README: command list, a PCbake paragraph, the matching, colour,
  naming and lot paragraphs, and the `PCMatch*`/`PCSpawn*` leftovers from the casing change.

## Deviations from plan

- The PLAN said the preselected path shows a separate `Bake` prompt; the implementation reuses the
  selection prompt once (same options, Enter accepts), which is the standard RhinoCommon pattern.
- `panel-cladding-pc-commands` also rejects the removed names in source and installer, ignoring
  full-line comments.

## Discovered issues

- `panel-cladding-object-names` checked naming inside the removed batch spawn entry; the first
  full Debug run failed on it (77/78). The check was removed (PCbake's naming is checked on the
  update service in the same smoke) and the full suite was rerun.
- Git Bash `sed -i` again rewrote a file to LF; restored to CRLF.

## Test record

- Targeted Debug run of the 13 affected smokes plus `pcbake-command`: 12/13, then
  `panel-cladding-update-command` passed after its `Existing` helper recorded the owning panel
  (the live service always does).
- First full run: Debug 77/78 (`panel-cladding-object-names`, see above), Release 78/78.
- Full rerun after the fix: Debug 78/78, Release 78/78, exit 0 (summaries in the TEST folder).
  `git status` showed no tracked change outside this edit set.
- `dotnet build .\PanelCladding.sln -m:1 --no-incremental`: Debug and Release, 0 warnings, 0 errors.
- `Verify-PluginAssemblyIdentity.ps1 -Configuration Release`: `[OK]`, plug-in id
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`.
- Release RHP `1.3.0+0f308e1` (the build stamps the last commit), SHA-256
  `CF565E943CBFA789FE12C10392C5CE55045CEAE4E647F0D8ACB2421FE4BD192C`; not packaged or installed.

Not run: bundle-based package checks (packaging is not part of this change) and anything in Rhino.

## Acceptance alignment

- Scope filtering, curve preservation, default `Surfaces`, 11 commands with the naming rule, and
  installer/check lists: met by managed smokes.
- Builds and both regression configurations: met.
- Live (pending an installed 1.3.0): a default run touches only surfaces; `Scope=All` leaves a
  panel's existing curves unchanged and reports it; the `Scope` option appears after preselection.

## Rollback

Revert the branch; `PCupdate`, `PCspawnSrf` and `PCspawnCrv` return. No document data changes shape.

## Remaining limitations

- The interactive prompt (option list, preselection pass, Enter) is verified by source contract
  only; it needs a Rhino run.
- A panel's curves can only be rebaked after deleting them; there is no rebuild option yet.
- Aliases, toolbar buttons and macros that call `PCupdate`, `PCspawnSrf` or `PCspawnCrv` stop
  working and must be changed to `PCbake`.

## Conclusion

`PCbake` replaces `PCupdate` and the two spawn commands, bakes surfaces by default with an
explicit `Scope` for curves or both, and never overwrites existing curves. The managed suite passes
in Debug and Release; packaging, installation and live Rhino acceptance remain for the release.
