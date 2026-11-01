# Live acceptance tests EXET

## Corresponding plan and execution date

Executed 2026-10-31 under [the user-authorized PLAN](../Project_Plan/261031_PLAN_live-acceptance-tests.md).

## Related artifacts

[TEST evidence](../Project_Test/261031_TEST_live-acceptance-tests/README.md); code in
[`tests/Live`](../tests/Live/README.md). Branch `codex/live-acceptance-tests` (local), worktree
`C:\01_Projects\Panel Cladding Full\PC-casing`.

## Implemented scope

- `tests/Live/PcLive.cs`: one class for a C# script host in Rhino (run through MCP_Rhino's live
  scripting with `libraryPath`/`libraryType`). Suites `commands`, `bake`, `excluded`, `report`,
  `scan`, `export`, `editor`, `otherdocs`, `probe`, joinable with `+`. Steps run one per
  `RhinoApp.Idle` tick, outside the host's undo record; a 250 ms no-op message keeps Idle coming.
  Commands go through `RhinoApp.RunScript`; panels are picked by preselection or transparent
  `'_SelID`; WPF windows and plug-in types are reached by reflection (the host references neither);
  windows are rendered off screen and viewports captured. Results go to `<evidenceDir>\<suite>.json`.
- `Watch-Dialogs.ps1`: finds the Rhino process's `#32770` dialogs with `EnumWindows`, reads them
  with UI Automation, presses the answer with Invoke or the `WM_COMMAND` the button would send.
- `Hold-Workbook.ps1` (Excel COM, hidden), `New-NamingSheet.ps1` (minimal naming sheet),
  `Restart-Rhino.ps1` (normal close and relaunch, waits for the MCP_Rhino route),
  `Show-Results.py`, `README.md`; `tests/README.md` has a `Live/` row.

## Deviations from plan

- BKT ran in a second Rhino on `BKT - Wireframe LIVE TEST.3dm` (the user replaced the older BKT copy),
  so the MET session was never replaced and the user was not asked to switch models.
- The naming-convention item was tested on a main-model-named scratch copy of MET with a minimal
  sheet, instead of being marked not testable.
- Dialog buttons in this Windows build are exposed to UI Automation as panes without Invoke, so the
  watcher sends the button's `WM_COMMAND` to its dialog (a window message; no input, no cursor move).

## Discovered issues

Plug-in findings are in the TEST record (BKT "No owner panel" on hand-drawn Mullion Wrap/Snow Guard
curves; GLS copies as Info; excluded-layer error masked by a stale extent; clipped Detail column).
Test harness lessons:

- `RhinoDoc.Undo()` called from an idle step (outside a command) left the document with an open undo
  record; MCP_Rhino then refused every script (`SCRIPT_REQUIRES_IDLE_DOCUMENT_WITH_UNDO_ENABLED`) and
  only a Rhino restart cleared it. Steps now run the `_Undo` command.
- A Rhino started from the agent's sandboxed shell read the redirected registry (stale MCP_Rhino
  `FileName` 1.3.7) and did not load MCP_Rhino. Rhino is started from the Terminal panel.
- From the sandbox, UI Automation saw a nameless helper `#32770` instead of the Save dialog; the
  watcher is started from the Terminal panel and finds dialogs by `EnumWindows`. `BM_CLICK` is ignored
  while a dialog is not active, hence `WM_COMMAND`. During the first export attempt two Save dialogs
  were closed outside the watcher; that run was discarded and the export rerun fully automated.
- Rhino's command history drops old lines; the runner finds new output after the last 400 characters
  of the old text.
- Test data: MET has duplicate PIDs (PCpid refuses) and PCpid refuses panels with generated
  dependencies; the suite suffixes duplicates (`-LIVEDUPn`) and numbers a new copy of a panel.

## Test record

Final runs: MET `bake` 18/18, `excluded` 14/14, `report` 10/10, `export` 2/2, `commands+editor` 18/18;
BKT/MET copy/new document `commands+scan+otherdocs` 15/15. No `src/` change; `Run-Regression.ps1` is
unaffected (`tests/Live` holds no smoke project).
