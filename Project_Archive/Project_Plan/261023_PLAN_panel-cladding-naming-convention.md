# Panel cladding naming-convention codes PLAN

## Background and authorization

On 2026-10-23 the user asked to reformat panel attributes and manage codes through
`ABC - Naming Convention.xlsx` (PNL sheet; rows 1–8 are the format/example, row 11
notes, lower rows are another project's values and are cleared). Decisions given:

- `CW_2.13_CLADDING_LOGIC` becomes `CW_9.01_CLADDING_LOGIC`.
- `CW_1.08_FRAME_CONFIG` holds a two-letter code (`AA`, `AB`, …, letters I and O
  unused). Same horizontal/vertical intermediate counts share the first letter; the
  second letter separates variants. The relationship is listed in the PNL sheet. The
  full configuration is kept, made readable, in `CW_9.02_FRAME_CONFIG` on each panel
  so layouts display correctly even when the sheet is locked.
- `CW_1.06_UNIT_TYPE` becomes C/F/L/Z (Curved, Flat, Two Face, Three Face).
  Parent/child moves to `CW_5.00_DEFINITION_A` (`Parent`, `Child`, `Child 1`, …);
  flat panels are `Parent`. Existing definitions shift down (A→B…, adding E+).
  CID role suffixes apply only to L/Z panels: `_P`, `_C`, or `_P`, `_C1`, `_C2`.
  ABC `TBD` unit types become Flat.
- Wall type: a code table in the sheet only; panels keep `WT-xx`.
- Every model save updates the sheet. Unused codes stay, struck through and grey.
  Codes are never reused. A locked sheet does not block the save; the next save
  catches up.
- The plug-in does not generate `CW_1.00_PNL`.

## Design

- `PanelFrameConfigFormat` (Application): lossless text
  `2H 1V | delete A1 0B | merge A0–1 | hide 0A`. Horizontal segments use track
  letter + bay number, vertical ones track number + bay letter, matching `INT-` curve
  codes. Readers accept it, the former JSON, and the retired mask keys.
- `PanelFrameConfigCodeRegistry` (Application): code ↔ configuration, assignment rule
  above, JSON persistence. Per document it is stored in the document string
  `PanelCladdingEditor.FrameConfigCodes`, seeded from panels and the sheet.
- Planners write only `CW_9.02`; the live write paths derive `CW_1.08` from it via
  `LivePanelFrameConfigCodes` (CID normalization already runs on each panel write).
  Topology change detection compares `CW_9.02` and ignores the derived code.
- `PanelCladdingCidService` reads unit type letters and Definition A roles.
  `PanelCladdingPidSetupService` reserves Definition A (shift, flat/curved → Parent).
- `OpenXmlPanelNamingConventionWorkbook` (Infrastructure/File) rewrites the PNL tables:
  Unit-Type E/F, Frame-Config I/J, new Wall-Type W/X; clears rows ≥ 12 elsewhere;
  unused rows struck through with grey font; temp copy + replace.
- `LivePanelNamingConventionSync` subscribes to `RhinoDoc.EndSaveDocument`; finds
  `<PROJECT> - Naming Convention.xlsx` beside the model (or the document string
  `PanelCladdingEditor.NamingConventionPath`), and reports success or a lock warning.

## Migration (ABC)

After 1.1.0 is installed, a Rhino script calls the loaded plug-in to convert each
panel: rename 2.13→9.01; legacy JSON → `CW_9.02` + assigned `CW_1.08` code (classes in
intermediate-count order, variants by usage); `flat`/`TBD` → F + Parent,
`corner_parent` → L + Parent, `corner_child` → L + Child. Then a model save writes
the sheet.

## Acceptance criteria

- Round-trip: every topology formats and parses back identically; legacy JSON still reads.
- Codes follow the grouping/alphabet rules and are stable and never reused.
- CIDs: F/C panels unsuffixed; L/Z panels `_P`/`_C`/`_Cn` from Definition A.
- Sheet: PNL rows ≥ 12 cleared; three tables written; unused rows struck/grey;
  other rows, merges and styles above row 12 unchanged; a locked file reports a warning.
- ABC read-back after migration: no `CW_2.13`, every configured panel has a code and a
  matching `CW_9.02`, unit types are C/F/L/Z, CIDs unchanged.
- Managed regressions and Debug/Release builds pass apart from pre-existing failures.

## Amendment (1.1.1, 2026-10-24)

After seeing the first sheet write the user set the PNL header order to Unit-Type |
Wall-Type | Frame-Config | Frame-Type | Cladding | Definition | Seq, with one-letter wall-type
codes, and asked the agent to rearrange the ABC sheet's rows once. Design: each table is found
by its title in row 12 (code column = title column, description = next column, rows from 13);
missing titles default to E, H, J; only those six columns are written so other tables the team
adds stay intact; the writer no longer leaves a `.panel-naming-backup` file. A one-time tool
rearranges ABC rows 1–11, adds the row-12 titles and moves the 1.1.0 tables, keeping their codes.

## Amendment (1.1.2–1.1.3, 2026-10-24)

1.1.2 restores `File.Replace` with an operation-owned backup (the project share rejects a
replace without one). On the user's request the project now comes from the Rhino file name
(`ABC - Wireframe.3dm` → `ABC`): `PanelCladdingProjectService` derives it; with a code only
`<code>-IEF-Wireframe` is the project root for PCpid scope, curve placement, managed-curve
discovery and the sheet sync; unsaved documents keep accepting any `*-IEF-Wireframe` root. The
save writes the code into the example-row cells under the row-7 `Job Code` header, and PCpid
offers it as its default project code.

## Risks and rollback

Older documents without migration keep working for reading (JSON fallback) but new
writes add codes. Rhino Undo reverts the migration before saving; the sheet keeps a
`.panel-naming-backup` during replacement. Revert the branch to restore 1.0.99.
