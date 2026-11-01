# Live acceptance tests TEST (1.3.0)

Evidence for [the PLAN](../../Project_Plan/261031_PLAN_live-acceptance-tests.md) and
[the EXET](../../Project_Exet/261031_EXET_live-acceptance-tests.md). The rerunnable code is in
[`tests/Live`](../../tests/Live/README.md). Every item of `PanelCladdingEditor-1.3.0 Manual Tests.md`
sections 1-5 was run against the installed plug-in without keyboard or mouse input.

## Environment (2026-10-31, NOVAN-HOME)

- Rhino 8.36.26293.11001. Every run's first step read the loaded `PanelCladdingEditor` assembly:
  `%LOCALAPPDATA%\PanelCladdingEditor\plugin\1.3.0\PanelCladdingEditor.rhp`, SHA-256
  `BDA62FFD1726D7BD6609A794081FDD1EF143BC2FB4C619447D6C3C96F14E8E39`, `1.3.0+150f546` (processes 51216
  and 68372).
- MET: `MET - Wireframe LIVE TEST.3dm` (MCP_Rhino release-1.3.9 test folder), 401 panels, process 51216.
- BKT: `BKT - Wireframe LIVE TEST.3dm` (same folder), 1,008 panels, 44,759 objects, in a second Rhino
  (process 68372) so the MET session was not replaced. The older `LiveTest-1.3.0\BKT\BKT - Wireframe.3dm`
  predates the layer rename (`01_CW Panels` ... roots, no `BKT-IEF-Wireframe`) and was dropped at the
  user's direction.
- Naming convention: a scratch copy of MET saved as `LiveTest-1.3.0\naming\MET - Wireframe.3dm` (a
  main-model name) beside a minimal sheet from `tests/Live/New-NamingSheet.ps1`.

## Results

| Suite (file) | Model | Checks |
| --- | --- | --- |
| `commands+editor` ([json](results/commands+editor.json)) | MET | 18/18 |
| `bake` ([json](results/bake.json)) | MET | 18/18 |
| `excluded` ([json](results/excluded.json)) | MET | 14/14 |
| `report` ([json](results/report.json)) | MET | 10/10 |
| `export` ([json](results/export.json)) | MET | 2/2 |
| `commands+scan+otherdocs` ([json](results-bkt/commands+scan+otherdocs.json)) | BKT, MET copy, new document | 15/15 |

Each results file lists every check with its evidence (command-line text, object states, counts)
and every macro sent with the history it produced.

### Per item

1. **Command names.** The plug-in registers exactly the 12 commands; Rhino's command list (the
   source of the `PC` completion) has no other `PC*` command or alias. `_PCEditor` and `_PCBAKE` run.
   `_PCupdate`, `_PCspawnSrf`, `_PCspawnCrv`: "Unknown command". No local alias and no loaded
   toolbar calls them (only `default.rui` is loaded; `settings-Scheme__Default.xml` keeps old
   `PCUpdate`/`PCSpawn*` entries under "Command Use Counts" only). Team-wide aliases and toolbars
   on other machines are not visible from here.
2. **PCbake** on panels of `PNL-EXT-3A.1` given a 1H 1V layout (four cells, MTL-001/GLS-001):
   preselection prompt shows `Scope=Surfaces`, Enter bakes 4 surfaces and 0 curves, curves untouched,
   preselection stays selected; without a preselection the prompt offers Scope, `Curves` creates 8
   curves only (frame 0,0,255; horizontal 128,0,128; vertical 0,100,0, colour from object), `All`
   creates 4 surfaces and 8 curves; the next run starts at Surfaces. A panel with curves, one of them
   moved and given `Extrusions`/colour by hand: `Curves` and `All` change none of them and report
   "Kept the existing curves of 1 panel(s)"; after deleting them `Curves` creates 8 again. Merging
   the cells: the default run updates 2 surfaces and deletes 2; one `_Undo` ("Undoing PCbake")
   restores every surface. A locked and a hidden surface are updated and stay locked and hidden.
3. **Not in Scope / By Others.** A panel moved to `Surfaces - PNL::Surfaces - Not in Scope` with stale
   auto values keeps them after save and reopen while an in-scope control is refreshed; renaming the
   layer to `Surfaces - By Others`, `NOT_IN_SCOPE` and `By-Others` (each rename triggers the same full
   scan) leaves them too. PCpid numbers 401 panels (400 + a new copy, the excluded one not counted),
   assigns only the new panel and leaves the excluded PID. PCbake `Curves` fails with
   `PANEL_CLADDING_PANEL_LAYER_EXCLUDED` under all four names and creates no curve. Saving the main-model
   copy writes the sheet with wall type `INS-LIVE` (in scope) and without `NIS-LIVE` (excluded panel).
4. **PCreport.** MET: 0 errors / 9,909 warnings / 1,207 info. No modified flag, and `_Undo` right after
   undoes the point drawn before PCreport (Rhino reserves an undo serial for every command, read-only
   ones too). Selecting a row selects that object and centres the view on it; three rows select three
   objects; locked and hidden objects are zoomed to but not selected; the layer-level row (`Not tracked
   layer`) selects nothing; naming one panel and Rescan drops "Name does not match CID" from 397 to
   396. `_SelNone` runs while the window is open (not thread-modal). Export offers
   `MET - Wireframe LIVE TEST - PC Report` in the model folder; the workbook has Summary (18 rows) and
   Issues (11,116 rows: header and 11,115 issues after the Rescan fix); exporting while Excel holds it shows
   `PANEL_CLADDING_REPORT_EXPORT_FAILED: Access to the path is denied.` with no leftover file. After
   opening another model, PCreport closes the BKT window and shows the other model; in an unsaved new
   document it prints "PCreport requires a saved active document." BKT: 1,008 panels in 0.5-1.1 s.
5. **PCeditor** on a `PNL-EXT-3A.4` panel with `UD_FRAME_ASSIGNMENTS` `FRM-0 -> 1D-T000`: Extrusion view
   shows Merge, Explode, Hide, Add H, Add V, Delete and the offsets; no catalogue, no floating
   assignment panel, no FRAME CONFIGURATION note or profile tooltip, no codes on curve labels (frame and
   intermediate segments selected). Hide + Save Extrusions keeps `FRM-0: 1D-T000` (payload now v2 with
   the profile definition). Cladding view keeps its legend and cell assignment. After Save, closing asks
   nothing; with an unsaved Hide, closing asks "Discard unsaved panel cladding edits?": No keeps the
   editor, Yes closes it.

## Findings

| # | Section | What happened | Expected | Outcome |
| --- | --- | --- | --- | --- |
| 1 | 4 PCreport (BKT) | 2,423 "No owner panel" warnings: 2,358 are hand-drawn curves under `Curves - PNL::Curves - Mullion Wrap` (`do not include` 1,204, `Opaque vertical mullion` 798, `... optional` 356), 65 under `Curves - Snow Guards::MTL-*`. Everything under `Curves - PNL` is treated as a managed dependency; `do not include` is not an excluded keyword. | Decide: track these layers as excluded (keyword or rename to By Others / Not in Scope), or keep the warnings. | User (2026-10-31): model issue; these curves do not belong on that layer. No plug-in change. |
| 2 | 4 PCreport (BKT) | `Surfaces - GLS` copies: 1,848 Info rows (not ~2,400), part of 7,627 "PID outside the managed layers" (TER-001 5,175, MP 339, STN 263). | User decision: acceptable as Info? | Changed on `codex/live-test-followups` (26cb82c): the copies keep their PID and layer, so PCreport no longer reports objects the plug-in does not manage (rule D07 removed). |
| 3 | 3 / 2 PCbake | On a Not in Scope panel whose `UD_PLANE` and sizes are stale (they are no longer refreshed there), PCbake reports `PANEL_CLADDING_PANEL_EXTENT_INVALID` instead of `PANEL_CLADDING_PANEL_LAYER_EXCLUDED`; the extent is read before the layer is checked. No curve is created either way. | The excluded-layer error first. | Fixed on `codex/live-test-followups` (0f2f792): auto values are refreshed on excluded panels again; no curves for them stays. |
| 4 | 4 PCreport window | At the default width the Detail column is cut off; it needs horizontal scrolling. | Detail visible. | Fixed on `codex/live-test-followups` (0f2f792): Detail fills the remaining width. |

Not plug-in defects, recorded for the test data: the MET model has 7 duplicate PID groups (PCpid
refuses until they are resolved; the suite suffixes `-LIVEDUPn`, which shows as 7 A05 warnings) and
`CW_1.08_FRAME_CONFIG 'A'` values that do not fit the `AA` format (message on every write).

## Evidence files

| File | Contents |
| --- | --- |
| `results/*.json`, `results-bkt/*.json` | Suite results (checks, notes, macros with their command-line output). |
| `results/report-window.png`, `report-click-zoom.png`, `report-export-error.png` | MET report window; viewport after a row click; window after the failed export. |
| `results/report-rules.json`, `results-bkt/report-rules.json` | Rule counts, per-layer counts and samples for MET and BKT. |
| `results/MET - Wireframe LIVE TEST - PC Report.xlsx`, `excel-sheets.json` | Exported workbook and the sheets Excel reported. |
| `results/export-dialogs.json`, `editor-dialogs.json` | Dialogs answered by `Watch-Dialogs.ps1`: title, text, offered file name and folder, button pressed. |
| `results/editor-cladding-view.png`, `editor-extrusion-view.png` | PCeditor renders (Extrusion view with a frame and an intermediate segment selected). |
| `results-bkt/report-window-BKT.png`, `report-window-other-model.png` | BKT window; the window after PCreport in the MET copy. |
| `results-bkt/MET - Naming Convention.xlsx` | The sheet written when the main-model copy was saved. |

## Commands

See [`tests/Live/README.md`](../../tests/Live/README.md). Suites run in this order on MET:
`bake`, `excluded`, `report`, `export` (with `Watch-Dialogs.ps1 -Answers Save,Save,Yes` from a normal
shell and `Hold-Workbook.ps1`), `commands+editor` (with `-Answers No,Yes`); on BKT in a second Rhino:
`commands+scan+otherdocs` with `mainModel` = the naming copy.
