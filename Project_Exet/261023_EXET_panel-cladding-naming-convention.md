# Panel cladding naming-convention codes EXET

## Corresponding plan and execution date

Executed 2026-10-23 under
[the user-authorized PLAN](../Project_Plan/261023_PLAN_panel-cladding-naming-convention.md).

## Related artifacts

[Scoped TEST](../Project_Test/261023_TEST_panel-cladding-naming-convention/README.md).
Same working branch `codex/panel-cladding-layer-standard`, on top of the uncommitted
layer-standard and ID-separator changes; no commit or PR was requested or created.

## Implemented scope

- `PanelFrameConfigFormat`: lossless text `<n>H <n>V | delete … | merge … | hide …`
  (segments `A1`/`0B`, runs `B0–1`/`0A–C`, hyphen accepted), parse with
  `PANEL_FRAME_CONFIG_TEXT_INVALID`.
- `PanelFrameConfigCodeRegistry`: two-letter codes without I/O; equal H/V intermediate
  counts share the first letter; codes are never reassigned; JSON persistence in the
  document string `PanelCladdingEditor.FrameConfigCodes`.
- `PanelCladdingKeyService`: `CW_9.01_CLADDING_LOGIC` (retired `CW_2.13_CLADDING_LOGIC` is
  a clearable key), `CW_9.02_FRAME_CONFIG`. Planners write only `CW_9.02`; topology change
  detection ignores the derived `CW_1.08`. Decoding prefers `CW_9.02`, then readable text in
  `CW_1.08`, then the former JSON and mask keys; a code without backup fails with
  `PANEL_FRAME_CONFIG_BACKUP_MISSING`, a grid mismatch with `PANEL_FRAME_CONFIG_GRID_MISMATCH`.
- `LivePanelFrameConfigCodes`: on every live panel write (through
  `LivePanelCladdingCidService.Normalize(document, attributes)`: save, create, match, surface
  sync, PCpid, curve template) the canonical text is written to `CW_9.02` and its code to
  `CW_1.08`; new codes are persisted to the document string immediately.
- `PanelCladdingCidService`: unit types C/F/L/Z; L/Z take `_P`, `_C`, `_C<n>` from
  `CW_5.00_DEFINITION_A` (`Parent`, `Child`, `Child <n>`); C/F have no suffix; other values,
  including the retired `flat`/`corner_*`, keep the stored CID.
- `PanelCladdingPidSetupService.ReserveRoleDefinition`: a non-role Definition A shifts the
  definitions down (adding E, F, …); C/F panels are always `Parent`.
- `PanelNamingConventionService` + `OpenXmlPanelNamingConventionWorkbook`: PNL rows 1–11
  kept, rows from 12 regenerated with Unit-Type (E/F), Frame-Config (I/J) and Wall-Type
  (W/X, header W7 merged W7:X7); merges from row 12 removed; unused rows struck through in
  grey (fonts/formats reused); sheet-only rows with valid config text stay reserved, other
  rows are dropped; wall values get letter codes A, B, … (no I/O); temp copy + `File.Replace`;
  no write when the sheet already matches; a locked file returns
  `PANEL_NAMING_CONVENTION_LOCKED` and is left unchanged.
- `LivePanelNamingConventionSync` (registered in `PanelCladdingEditorPlugin.OnLoad`): after
  each `.3dm` save (not export-selected or autosave) reads panels under
  `<PROJECT>-IEF-Wireframe::Surfaces - PNL`, finds `<PROJECT> - Naming Convention.xlsx`
  beside the model (or `PanelCladdingEditor.NamingConventionPath`), writes the tables and
  reports. Used frame codes come from `CW_9.02`; panels whose `CW_1.08` disagrees are counted
  in a warning. The document is not modified by the hook. `CloseDocument` drops the cache.
- No `CW_1.00_PNL` generation. Version 1.1.0; package README section.

## Deviations from plan

- Added after review: no-op write detection (avoids a SharePoint version and a lock warning
  on every save), C/F always `Parent`, the code/config mismatch warning, calc-chain removal.

## Test record

- New `261023_TEST_panel-cladding-naming-convention`: 7 checks pass in Debug and Release,
  including Open XML SDK schema validation of the written workbook.
- 14 projects asserted the former JSON in `CW_1.08` or `flat`/`corner_*`: topology
  persistence, hide mask, curve topology, scoped save, sparse topology, create command,
  extrusion assignment (frame checks now on `CW_9.02`; malformed cases rebuilt from real
  legacy JSON plus readable-text cases), and id-separators, role-cid, object-names, pid,
  pid-setup-keys, update-command, canvas-assignment (L/Z/F/C with Definition A; retired values
  asserted as unsupported). Native PCpid/point-order smokes moved to F/L (not run here).
- Final: Debug 68/75 and Release 68/75; the 7 failures are the pre-existing ones with the
  same first messages as the 1.0.99 baseline. `Verify-Regression.ps1` passes; Release identity
  GUID `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`. `git diff --check` clean.
- Dry run of the writer on a copy of `ABC - Naming Convention.xlsx`: 0 schema errors before
  and after; rows 1–11 unchanged; the other project's rows and `R12:U16` removed.
- Package `PanelCladdingEditor-1.1.0-win-x64.zip` (SHA-256
  `e775757c2e309065bb4c281225df1919de7d4d693fccb43bac27e3bc0af1f9e6`) passed
  `Test-Package.ps1` (32 payload hashes, identity, isolated lifecycle). Staged at
  `C:\01_Projects\Panel Cladding Full\PanelCladdingEditor-1.1.0`; RHP SHA-256
  `138574c2ef9b6701c1ce218aba74656e0b4895e94f51bcfe45745959ef96403e`, version 1.1.0.
- Regenerated historical screenshots were restored after each run.

## Acceptance alignment

Format round trip, code rules, CID roles, Definition A and the sheet behaviour are covered by
the smoke. ABC migration, installation and the first sheet write are pending (below).

## Rollback verification

Source rollback is reverting the branch. 1.0.99 stays installed until the user installs
1.1.0. Before saving, Rhino Undo reverts the ABC migration in one step; the workbook is
replaced only after a complete temporary copy is written.

## Installation (2026-10-24)

The user ran the staged `Install.cmd`. Independent StdRegProv read-back: `PlugIn\FileName` =
`%LOCALAPPDATA%\PanelCladdingEditor\plugin\1.1.0\PanelCladdingEditor.rhp`, SHA-256 `138574c2…403e`
(tested RHP); exact 12-command `CommandList`; root, `PlugIn` and `CommandList` times advanced
past the pre-install snapshot; `-Mode Validate` passed; `install-manifest.json` = 1.1.0 with a
1.0.99 rollback copy. In Rhino the single loaded `PanelCladdingEditor` assembly is 1.1.0 from
that path and the save hook is registered.

## Document migration (ABC - Wireframe.3dm, live route, Rhino PID 19552)

Before: 1,144 panels; unit types `flat` 950, `corner_parent` 44, `corner_child` 44, `TBD` 9,
none 97; `CW_1.08` JSON 907, `TBD` 61, blank 79, none 97; no codes document string; document
unmodified. A dry run through the loaded assembly produced the 33-code table, which the user
reviewed and approved. Apply (one tool-owned Undo record): 1,047 panels modified, 0 failures —
`CW_2.13`→`CW_9.01` on 901, JSON→`CW_9.02` + code on 907, `flat`/`TBD`→F + Parent,
`corner_parent`→L + Parent, `corner_child`→L + Child; no CID or name changes; the code table
stored in `PanelCladdingEditor.FrameConfigCodes` and the plug-in cache reset.

Independent read-back: 33 document codes = live plug-in cache; 0 objects with `CW_2.13`, 901
with `CW_9.01`; 907/907 coded panels have a code whose table entry equals `CW_9.02`, decode with
their own grid and re-canonicalize identically; no JSON left; F 959 (all Parent), L 88 (44
Parent, 44 Child), 97 without unit type; 0 CID and 0 name mismatches. Unchanged by design:
61 `TBD` and 79 blank frame configs (no code), 97 panels without unit type.

## First sheet write (1.1.0, 2026-10-24)

The user saved ABC (00:55Z) and closed Rhino. The save hook wrote the PNL sheet in the 1.1.0
layout: Unit-Type E/F (C and Z struck), 33 frame configs in I/J, 9 wall types in W/X (`A`=`04`,
`B`..`H`=`WT-01`..`WT-08` without `WT-05`, `J`=`WT-11`). The user then opened and saved the
workbook in Excel (00:57Z; inline strings converted, styles compacted 78→55). The reopened
model read back with 33 codes, 0 `CW_2.13`, 901 `CW_9.01`, 907 `CW_9.02`. Defect: the writer
left `ABC - Naming Convention.xlsx.panel-naming-backup` (the pre-write workbook) next to the
sheet because its delete after `File.Replace` failed on the share; it was not removed by the
agent.

## Follow-up 1.1.1: PNL layout

See the PLAN amendment. `OpenXmlPanelNamingConventionWorkbook` now locates the tables by their
row-12 titles, writes only those six columns from row 13, rejects overlapping/duplicate titles
(`PANEL_NAMING_CONVENTION_LAYOUT_INVALID`), takes row styles from the table's rows or the title
without bold, and calls `File.Replace` without a backup file. The smoke's workbook check was
rewritten for this layout (moved/missing/overlapping/duplicate titles, untouched Cladding table
and merges). A one-time scratch tool rearranges ABC rows 1–11 (Wall-Type inserted in I with its
own fill, Frame-Config J:K, Frame-Type L:M, Cladding N:O, Definition P:Q, Seq S:V; REVIT FAMILY
H5:O5, CORE PNL H6:Q6; column widths moved with their columns), adds the titles and rewrites
the captured tables through the 1.1.1 writer. Dry run on a copy of the Excel-saved sheet: 4/33/9
rows captured and rewritten with the same codes and struck state, 0 schema errors.

Tests: naming smoke passes; full suite Debug 68/75 and Release 68/75 with the same 7
pre-existing failures; `Verify-Regression.ps1` passes; `git diff --check` clean. Package
`PanelCladdingEditor-1.1.1-win-x64.zip` (SHA-256
`d6bd7363a2f454b4be8836b56d2783130c37b4e5d97180a9b42c217eed400bf7`) passed `Test-Package.ps1`;
staged at `C:\01_Projects\Panel Cladding Full\PanelCladdingEditor-1.1.1`, RHP SHA-256
`86b3363c68026bf649b3586bc1822beee583d1f13725977978b6d899e09395df`.

## Wall-type fix, 1.1.1 installation and layout change (2026-10-24)

The user changed the 9 `04` panels to `WT-04` (read-back: 153 `WT-04`, all on
`Surfaces - WT_04`, no `04`) and saved with 1.1.0 (01:18Z), which rewrote the old layout with
wall code `A` (`04`) struck as unused. The user then installed 1.1.1. Independent read-back:
`PlugIn\FileName` = `…\plugin\1.1.1\PanelCladdingEditor.rhp`, SHA-256 `86b3363c…95df`, exact
12-command `CommandList`, root/`PlugIn`/`CommandList` times advanced, `-Mode Validate` passed,
Rhino closed. With Rhino closed the sheet was backed up to the agent scratchpad (SHA-256
`AF904903…6DD1`), the layout tool ran on a working copy (4/33/9 rows, 0 schema errors; A, C, Z
struck), the real file was confirmed unchanged since the backup and replaced with the copy.
The user opened and saved ABC (01:25Z): the sheet stayed byte-identical (the 1.1.1 writer
found it current), no temporary or new backup files appeared, and in Rhino the only loaded
`PanelCladdingEditor` is 1.1.1 from the installed path with the save hook registered; it reads
33 frame configs and 9 wall types from the new layout.

## Wall code re-letter and 1.1.2 share fix (2026-10-24)

The user said `04` was a data mistake: delete it instead of striking it, and re-letter the
remaining wall codes from A (wall codes exist only in the sheet; panels keep `WT-xx`). Applying
that through the loaded 1.1.1 writer in Rhino failed with `PANEL_NAMING_CONVENTION_LOCKED`
("Unable to remove the file to be replaced") although the file was not open. A .NET 8 probe
with operation-owned files in the sheet folder showed the share rejects `File.Replace` without a
backup (0x80070497) while `File.Replace` with a backup and deleting that backup succeed; 1.1.1
had removed the backup, so every real sheet change from 1.1.1 would fail (its one save after the
layout change was a no-op). 1.1.2 replaces with a unique hidden backup
(`.<name>.naming.<guid>.bak.tmp.xlsx`), deletes it with up to five retries, and reports
`PANEL_NAMING_CONVENTION_BACKUP_LEFT` if one remains. The naming smoke passes in Debug and
Release; `Verify-Regression.ps1` passes.

After a scratch backup of the sheet, the 1.1.2 writer (scratch tool, usage from the live model:
F/L, 33 frame codes, `WT-01`…`WT-11`) wrote A=`WT-01`, B=`WT-02`, C=`WT-03`, D=`WT-04`,
E=`WT-06`, F=`WT-07`, G=`WT-08`, H=`WT-11` on the share: success, no backup or temp file left,
second write `UNCHANGED`; only C and Z struck; all wall codes carry the Wall-Type fill.

Package `PanelCladdingEditor-1.1.2-win-x64.zip` (SHA-256
`e74adef005c3947565c768c064d9da4b7c4ceee090922d09509d5129437deda1`) passed `Test-Package.ps1`;
staged at `C:\01_Projects\Panel Cladding Full\PanelCladdingEditor-1.1.2`, RHP SHA-256
`7a3c2b23040d0cc39e161344736bcde6c2e450d3280992f2f66abc22474d6ac5`.

The user installed 1.1.2. Independent read-back: `PlugIn\FileName` =
`…\plugin\1.1.2\PanelCladdingEditor.rhp`, SHA-256 `7a3c2b23…6ac5`, exact 12-command
`CommandList`, root/`PlugIn`/`CommandList` times advanced, `-Mode Validate` passed; Rhino closed;
sheet unchanged since the re-letter write.

## Colours and layout style correction (2026-10-24)

On the user's request the segment fills were brightened (resolved by cell, not style index):
Group `FF8A65`, Unit-Type `4DD0E1`, Wall-Type `FFE066`, Frame-Config `FFB74D`, Frame-Type
`7986CB`, Cladding `AED581` (no longer sharing Frame-Config's hue), Definition `4DB6AC`, Seq
`F06292`; table code cells follow their segment. The audit for this found a defect in the
one-time layout tool: it applied style indices taken from the original workbook to a copy
Excel had already renumbered, so I2, I3, I5, L5, M5 and I6 carried wrong formats (missing
bottom borders under the title and CORE PNL at I, Unit-Type fill inside REVIT FAMILY). Those
cells now copy the style of their correct neighbour (H2, H3, J5, J6). Each segment fill is used
only by that segment's cells; 0 schema errors; the sheet was confirmed unchanged and not open
before replacement. The writer keeps these colours because it takes row styles from the sheet.

## 1.1.3: project from the file name and Job Code (2026-10-24)

The user asked that the sheet's Job Code (still the template's `R E G`) and the
`XXX-IEF-Wireframe` root both come from the Rhino file name. `PanelCladdingProjectService`
derives the code (text before the first ` - `, up to the first non-alphanumeric character,
upper case; empty when unsaved). Layer predicates gained `(layerPath, projectCode)` overloads
and curve planning an optional `projectCode`; PCpid scope and messages, spawn curve placement and
conflict scope, update dependency discovery, surface/curve sync and the save hook pass the code of
their document, so only `ABC-IEF-Wireframe` counts in `ABC - Wireframe.3dm` (any root when unsaved).
The writer finds the row-7 `Job Code` header and its merged span and writes the code into the
example-row cells (one letter each, or whole in a single cell; a code that does not fit is reported
once and skipped), keeping their styles; the up-to-date check includes it. PCpid offers the file
code as its default. Tests: layer-standard smoke gained the file-name rule and project-scoped
predicates/curve planning; naming smoke gained Job Code read width, write, change, non-fit, single
cell and no-header cases; three source-scan smokes now expect the predicate called with the code.
Dry run on a copy of the real sheet: Job Code width 3 → `A B C`, tables unchanged, second write
`UNCHANGED`, 0 schema errors.

Full suite Debug 68/75 and Release 68/75 (the same 7 pre-existing failures); `Verify-Regression.ps1`
passes; `git diff --check` clean. Package `PanelCladdingEditor-1.1.3-win-x64.zip` (SHA-256
`cfe91484968630748985a7b6f53a25c414df366f8e1e8102a867d2b886dc29d8`) passed `Test-Package.ps1`;
staged at `C:\01_Projects\Panel Cladding Full\PanelCladdingEditor-1.1.3`, RHP SHA-256
`951d9cc149bc46dc7e3160cc8267f6b95c3fe2436cc60350cf93047f0fa5bbaf`.

The user installed 1.1.3 (read-back: `…\plugin\1.1.3\PanelCladdingEditor.rhp`, SHA-256
`951d9cc1…bbaf`, 12 commands, three key times advanced, `-Mode Validate` passed) and saved ABC
(01:56Z): the sheet's Job Code reads `A B C`. Defect: the save left
`.ABC - Naming Convention.naming.<guid>.bak.tmp.xlsx` (the pre-write sheet). V: is an Egnyte drive
(`\\SyncDrive\Projects`) whose sync client briefly holds a just-created file, so the backup could
not be deleted even with retries; minutes later nothing held it.

## 1.1.4: move-over write (2026-10-24)

The writer now moves the finished temp copy over the sheet in one step (`File.Move` with overwrite,
as Excel saves), so no backup file exists; an open workbook refuses the move and is reported as
locked. Each save also removes operation-owned `.<name>.naming.*.tmp.xlsx` files older than a minute
(the current leftover is removed by the next save). Naming smoke (new stale-file check) passes in
Debug and Release; `Verify-Regression.ps1` passes. Probe on the Egnyte drive with an operation-owned
copy of the sheet: write succeeded, second write `UNCHANGED`, open file → `PANEL_NAMING_CONVENTION_LOCKED`,
no leftovers. Package `PanelCladdingEditor-1.1.4-win-x64.zip` (SHA-256
`6999a60ea5feed52bd46d12bec6332548925f9a2ccdddfdbd30f263c1c1ec2a1`) passed `Test-Package.ps1`;
staged at `C:\01_Projects\Panel Cladding Full\PanelCladdingEditor-1.1.4`, RHP SHA-256
`e93dea9e8f185b5ac647c7d0eb4a7dfa77ac9ec2358f2e4a4d9f6558f9c84f1f`. The full suite was not rerun for
this writer-only change.

## Remaining items

Install 1.1.4; the next ABC save removes the leftover backup file. The 1.1.0 leftover `ABC - Naming Convention.xlsx.panel-naming-backup` is still beside the
sheet for the user to remove. No commit was requested.

## GitHub publication follow-up (2026-10-27)

Source commit: `971f652e7565c083af77ce55dac70001e3f2b5cf`.
Pull request: [#2 — project layer standard, ID separators and PNL naming convention](https://github.com/novan-xu/PanelCladding/pull/2).
