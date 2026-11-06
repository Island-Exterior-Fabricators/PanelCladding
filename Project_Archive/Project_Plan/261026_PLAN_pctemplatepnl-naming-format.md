# PCtemplatePNL per-project PNL format PLAN

## Background and authorization

On 2026-10-26 the user (through the MCP_Rhino session that started this one) asked for a
new command `PCtemplatePNL` that records each project's PNL code format, and for the save-time
naming-convention sync to follow it instead of ABC's hard-coded format. Context:

- PNL marks are `PNL-<JOB>-<unit><wall><frameConfig><frameType><cladding><definition>-<seq>`.
  ABC uses wall `A`, frame config `AA`, frame type `01`, cladding `01`, definition `01`.
  XYZ (XYZ) uses wall `A`, frame config `A`, frame type `01`, cladding `001`,
  definition `01`; seq is four digits; unit type is fixed (C/F/L/Z).
- The 1.1.4 save hook rewrote `XYZ - Naming Convention.xlsx` on the STN model save at 16:02:
  Frame-Config `A` = `0H 0V` became `AA`, the user's placeholder rows (J14–J36 B..Z, H26–H36
  P..Z) were dropped, I25 `5` became the text `'5'`. The user restored `A` by hand.
- Standing user rule: assigned codes never move; only affected items change.

User decisions in this session: Definition is not prompted. It is always two characters:
`01`..`99`, then letter+digit, then letter+letter when more are needed.

The request authorizes the PLAN -> EXET -> TEST records, the build, a staged package and
configuring the XYZ sheet for the user.

## Goals

1. `PCtemplatePNL` prompts for Wall-Type, Frame-Config, Frame-Type and Cladding, one at a
   time; each answer is required and must be one of `AA`, `A`, `A1`, `01`, `001`
   (re-prompt otherwise). Letters I and O are never used.
2. It configures the PNL sheet: the example row 8 (one cell per character of each segment,
   merged across the segment when the token is one character), the segment columns, and
   each table's code column from row 13 with placeholder codes in that format.
3. It records the format in the workbook so both models of a project share it.
4. The save-time sync follows the recorded format and updates its tables in place.

## Architecture ownership

- Application: `PanelNamingFormat` (tokens, code sequences, validation, serialization);
  `PanelFrameConfigCodeRegistry` takes the frame-config token; `PanelNamingConventionService`
  numbers new wall types in the wall token.
- Infrastructure/File: `OpenXmlPanelNamingConventionWorkbook` (sync tables, reads the
  format), new `OpenXmlPanelNamingTemplate` (layout, example row, placeholders, format
  property).
- Infrastructure/Rhino/Live: `LivePanelNamingFormat` (per-document format from the sheet,
  with a document-string mirror), `LivePanelFrameConfigCodes`, `LivePanelNamingConventionSync`.
- UI: `PanelCladdingTemplatePnlCommand` (`PCtemplatePNL`).

## Design

### Format record

- Stored as the workbook custom document property `PanelCladdingEditor.PNLFormat`, value
  `Wall-Type=A; Frame-Config=A; Frame-Type=01; Cladding=001; Definition=01`. A custom
  property lives in the workbook, so both models of a project read the same value; Excel
  keeps it on save, and it does not occupy or depend on any cell the team edits. A sheet
  without the property uses the legacy ABC format (`A`, `AA`, `01`, `01`, `01`).
- Code sequences (letters without I/O, 24 letters):
  `A` A..Z (24); `AA` AA, AB, … ZZ (576); `A1` A1..A9, B1, … Z9, then AA..ZZ;
  `01` 01..99, then A1..Z9, then AA..ZZ (the user's Definition rule, applied to every
  two-digit segment); `001` 001..999. A code fits a token when it is in its sequence.
- Frame-config codes: `AA` and `A1` keep the 1.1.0 grouping (first letter = H/V intermediate
  counts, second character = variant); `A`, `01`, `001` take the first free code in order.

### PCtemplatePNL

- Saved document required; the sheet is `<PROJECT> - Naming Convention.xlsx` beside it (or
  `PanelCladdingEditor.NamingConventionPath`). The prompt shows the recorded format.
- An answer is rejected when a code already written with a description in that table does
  not fit it (codes never move); placeholders (a code without a description) are replaced.
- Layout (row 7 headers located by title, spans from their merges): Wall-Type is as wide as
  its token; Frame-Config, Frame-Type, Cladding and Definition are at least two columns (the
  code and description columns of the table beneath). A segment that is too narrow gains
  columns at its end (cells, merges, column widths shift; merges ending at the segment grow);
  one that is too wide loses its last columns only when they hold nothing outside rows 7–8.
  Sheets with formulas, conditional formats, validations, hyperlinks, tables, filters or
  drawings are not shifted (reported). Row 7 header spans are merged to the segment.
- Row 8: the token characters, digits as numbers; one-character tokens merged across the
  segment. Cells that already show the right character are left as they are.
- Tables: the row-12 title of each segment (Wall-Type keeps H, Frame-Config J; Frame-Type,
  Cladding, Definition default to the segment's first column, title added when missing).
  Rows 13–36 (24 rows) whose code and description cells are both empty get the next free
  codes; `AA`/`A1` frame-config tables get no placeholders (codes are grouped by class).
  Existing codes and every other cell are untouched. Definition is fixed `01` (not prompted);
  if its existing codes do not fit, Definition is left alone and reported.
- The format property is written; the document mirror and cached code tables are reset.
- Write: temp copy + one-step move, the same lock handling as the sync.

### Sync fixes

- `Read` returns the format; frame-config codes are read with its token. The registry and
  `IsCode`/`Resolve` follow the token; stored or panel codes that do not fit are ignored.
- Writing is a reconciliation in place, per sync table (Unit-Type, Wall-Type, Frame-Config):
  a code already in the column keeps its row; an empty description (placeholder) is filled;
  a non-empty description is never rewritten (a different model value is reported as a
  conflict); only the struck state of used/unused rows changes; a new code takes the first
  row below the table. No rows are cleared or reordered, merges are left alone, unchanged
  cells keep their type (numbers stay numbers). New numeric text is written as a number.
- The up-to-date check runs the same reconciliation without writing.
- `LivePanelFrameConfigCodes.Normalize` uses the project format. A panel's existing code that
  has a code shape but does not fit the current format is left as it is (reported once).
  The format is read from the sheet once per document session (refreshed by every save and
  by PCtemplatePNL) and mirrored in the document string `PanelCladdingEditor.PNLFormat`
  for when the sheet cannot be read.

## Affected files

`Application/Services/PanelCladding/PanelNamingFormat.cs` (new),
`PanelFrameConfigCodeRegistry.cs`, `PanelNamingConventionService.cs`,
`PanelCladdingKeyService.FrameConfiguration.cs`, `Domain/PanelCladdingNamingConventionModels.cs`,
`Infrastructure/File/OpenXmlPanelNamingConventionWorkbook.cs`,
`Infrastructure/File/OpenXmlPanelNamingTemplate.cs` (new),
`Infrastructure/Rhino/Live/PanelCladding/LivePanelNamingFormat.cs` (new),
`LivePanelFrameConfigCodes.cs`, `LivePanelNamingConventionSync.cs`,
`UI/PanelCladdingTemplatePnlCommand.cs` (new), package version and expected command lists
(installer, registration and release tests), package README.

## Usage

Run `PCtemplatePNL` in a saved project model, answer the four prompts (for XYZ: `A`, `A`,
`01`, `001`). Close the workbook in Excel first.

## Acceptance

- Managed smoke: tokens and sequences (I/O skipped, overflow chain), registry per token,
  template on generated ABC-like and XYZ-like workbooks (example row, merges, inserted
  columns, placeholders, untouched cells, schema-valid, idempotent), sync reconciliation
  (placeholders kept, numbers kept, struck state only, conflicts left, no-op on re-run).
- Debug and Release builds; full managed suite with only the known pre-existing failures.
- XYZ: the sheet carries the format; J13 stays `A` = `0H 0V` with B..Z placeholders; wall
  placeholders run to Z; every L–R value unchanged; the new sync with the STN model's usage
  leaves the sheet unchanged; all 96 STN panels keep `CW_1.08_FRAME_CONFIG = A`.
- Installation only with the independent registry attestation in AGENTS.md.

## Rollback

Revert the source files. The XYZ sheet is snapshotted before every write; the previous
workbook can be put back from the snapshot. 1.1.4 stays installed until the user installs
the new package.

## Future extensions

A per-project Seq width; templates for other sheets (REL); a sync that reads usage from all
models of a project instead of only the saved one.
