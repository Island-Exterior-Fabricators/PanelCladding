# PCreport command EXET

## Corresponding plan and execution date

Executed 2026-10-31 under [the user-authorized PLAN](../Project_Plan/261031_PLAN_pcreport-command.md).

## Related artifacts

[TEST evidence](../Project_Test/261031_TEST_pcreport-command/README.md) with the window render;
smoke [`tests/Smoke/pcreport-command`](../../tests/Smoke/pcreport-command/Program.cs). Branch
`codex/pcreport-command`, stacked on `codex/excluded-layer-keywords`, in the worktree
`C:\01_Projects\Panel Cladding Full\PC-casing`. Commit `dfd5833`;
[PR 13](https://github.com/nuofan-island/PanelCladding/pull/13), opened and merged into `master` at the
user's request after [PR 12](https://github.com/nuofan-island/PanelCladding/pull/12).

## Implemented scope

- Domain `PanelCladdingReportModels.cs`: severity, category, snapshot (layer paths; Brep/curve
  objects with id, name, layer and ordinal-keyed user text), rule, issue, report.
- Application `PanelCladdingComplianceService`: 27 rules (7 layer, 13 attribute, 7 dependency) as
  in the PLAN, each built on the plug-in's own predicates and services. Tracked panels are Breps
  in `IsPanelLayerPath`; dependencies are Breps under `Reference::Surfaces - STEP` and curves in the
  managed curve tree; ownership uses `PanelCladdingCidService.IncludesDependency` on the resolved
  panel CID. Issues are ordered by rule, layer and name. `IPanelReportNavigator` interface.
- Infrastructure: `LivePanelCladdingReportReader` (read-only snapshot, reusing
  `LivePanelCladdingCidService.Read` so case variants of a key stay apart),
  `LivePanelReportNavigator` (unselect, select, zoom to the union box, redraw),
  `OpenXmlPanelCladdingReportWriter` (Summary and Issues sheets, bold headers, temporary file then
  move).
- UI: `PanelCladdingReportWindow` (modeless, editor palette: model name and counts, Rescan,
  Export; rules grouped Layers/Attributes/Dependencies with severity dot and count; the selected
  rule's fix and an Object/Layer/Detail list; selecting rows calls the navigator; "No issues" when
  empty) and `PanelCladdingReportCommand` (`PCreport`, GUID `2CB10B8E-...`, one window per document,
  one summary line on the command line).
- Installer command list and package checks: 12 commands with `PCreport`; package README: command
  list, a PCreport section, twelve-command `CommandList`.

## Deviations from plan

None in behavior. The "layer name close to the standard" rule skips layers under the retired
roots (those are already reported as a retired tree).

## Discovered issues

- `panel-cladding-layer-standard` forbids the retired layer names anywhere in plug-in source; the
  report must name them to find them. The smoke now allows them only in the compliance service's
  `RetiredRootLayers` line (first full run: Debug 80/81, Release 80/81 on that smoke).
- `panel-cladding-pc-commands` rejects "PanelCladding" inside command-line output code; the
  summary line was rewritten with local counts.
- The package README still said the installer writes a thirteen-command `CommandList` (stale since
  PCbake left 11); it now says twelve, as does `Test-UserInstallation.ps1`.

## Test record

- `pcreport-command` (Debug): all 27 rules fire; compliant panel/surface/curve raise nothing;
  not-tracked panel skipped; missing root is an error; export 30 issue rows; window counts, 27 rules
  in 3 groups, navigator receives both duplicate-CID panels, layer rows select nothing; reader writes
  nothing.
- Full rerun after the fix: Debug 81/81, Release 81/81, exit 0; slowest smoke 2.3 s (no modal
  prompt waited). Summaries and the window render are in the TEST folder.
- `dotnet build .\PanelCladding.sln -m:1 --no-incremental`: Debug and Release, 0 warnings, 0 errors.
- `Verify-PluginAssemblyIdentity.ps1 -Configuration Release`: `[OK]`.
- Release RHP `1.3.0+00bd7ae`, SHA-256
  `B26A170EF698B62FBE55119EA817F28A0018D7F4CF9A34D5773ED8A3F5C5AE0D`; not packaged or installed.

## Acceptance alignment

Managed criteria met. Live (pending an installed 1.3.0): PCreport on ABC and XYZ opens the window
in Rhino, rows select and zoom to objects, Rescan reflects edits, Export writes the workbook.

## Rollback

Revert the branch; PCreport changes nothing in a model.

## Remaining limitations

- Not run in Rhino; the window was rendered off-screen only. Counts on real models (for example
  the ~2,400 ABC wireframe glass copies reported as Info) are not yet known.
- Auto values are checked for presence (`UD_PLANE`), not against measured geometry.
- No one-click fixes; rules are fixed in code.

## Conclusion

`PCreport` checks layer structure, panel attributes and managed surfaces and curves with 27 rules
that reuse the plug-in's own logic, and shows them in a modeless window with navigation and Excel
export. The managed suite passes in Debug and Release; live acceptance remains.
