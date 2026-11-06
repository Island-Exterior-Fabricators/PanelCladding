# PCtemplatePNL per-project PNL format EXET

## Corresponding plan and execution date

Executed 2026-10-26 under
[the user-authorized PLAN](../Project_Plan/261026_PLAN_pctemplatepnl-naming-format.md).

## Related artifacts

[Scoped TEST](../Project_Test/261026_TEST_pctemplatepnl-naming-format/README.md); the checks are in
[the naming-convention smoke](../../Project_Test/261023_TEST_panel-cladding-naming-convention/README.md).
Same working branch `codex/panel-cladding-layer-standard`, on top of the uncommitted 1.0.98–1.1.4
changes; no commit or PR was requested or created. Before editing, the naming-sync files were last
modified on 2026-10-23 and no other session was changing them.

## Implemented scope

- `PanelNamingFormat` (Application): tokens `AA`, `A`, `A1`, `01`, `001`; sequences without I/O;
  two-character tokens overflow `01`..`99` → `A1`..`Z9` → `AA`..`ZZ` (the user's Definition rule,
  applied to every two-digit segment); `Fits`, `LooksLikeCode`, `ReadToken`; format text
  `Wall-Type=A; Frame-Config=A; Frame-Type=01; Cladding=001; Definition=01`; `Legacy` = ABC's
  `A`, `AA`, `01`, `01`, `01`. Definition is always `01` and is not prompted (user decision).
- `PanelFrameConfigCodeRegistry(token)`: `IsCode`/`Resolve` follow the token; `AA`/`A1` keep the
  grouping by intermediate counts, `A`/`01`/`001` take the next free code; stored entries of another
  format are skipped. `PanelCladdingKeyService` recognises any format's code shape for
  `PANEL_FRAME_CONFIG_BACKUP_MISSING`.
- `PanelNamingConventionService.BuildTables(..., format)`: a new wall value takes the first free code
  of the wall format (the first placeholder when the sheet has them); values without a free code are
  reported (`UncodedWallTypes`).
- `OpenXmlPnlSheet` (new, Infrastructure/File): shared PNL sheet access, the custom property
  `PanelCladdingEditor.PNLFormat`, number-or-text cells, the temp-copy + one-step move write, the
  struck/plain style cache (moved from the workbook class).
- `OpenXmlPanelNamingConventionWorkbook`: `Read` returns the recorded format text and reads
  Frame-Config codes in its token. `Write` reconciles in place (codes keep their rows, placeholders
  filled, written values never replaced, struck state only, new codes below the table, merges and
  cell types kept); the up-to-date check runs the same reconciliation without writing.
- `OpenXmlPanelNamingTemplate` (new): `Inspect` (recorded format, codes with values per segment),
  `Misfits`, `Apply` (segment widths, row-7/row-8 merges, example characters with digits as numbers,
  row-12 titles, 24 placeholders, format property; column insert/delete moving cells, merges and
  widths; refuses sheets with formulas, conditional formats, validations, hyperlinks, filters,
  tables, drawings/comments or defined names on PNL when a shift is needed).
- `LivePanelNamingFormat` (new): per-document format from the sheet (once per session, refreshed by
  every save and by PCtemplatePNL), mirrored in the document string `PanelCladdingEditor.PNLFormat`
  when a code is assigned or the command runs.
- `LivePanelFrameConfigCodes`: registry in the project's token, seeded from the sheet's Frame-Config
  rows, the stored codes and the panels. Without a recorded format a panel code of another shape is
  left as it is (reported once) instead of being recoded in `AA`.
- `LivePanelNamingConventionSync`: reads the sheet first, then the model; conflicting sheet rows are
  reported and never overwritten; uncoded wall values reported; `ResolvePath` shared.
- `PanelCladdingTemplatePnlCommand` (`PCtemplatePNL`, GUID `C2F02782-3BB5-4604-8345-88B88C6DB9EE`).
- Version 1.2.0; installer, registration test and first-release installation test list 13 commands;
  package README section.

## Deviations from plan

None in behaviour. The checks were added to the existing naming-convention smoke (which owns the PNL
code) instead of a new project; the new TEST folder points to it.

## Test record

- Naming-convention smoke (with `Program.Template.cs`): all 12 checks pass in Debug and Release.
- `260818_TEST_panel-cladding-pc-commands` now expects 13 commands; passes in Debug and Release.
- Full managed suite (75 projects, `dotnet run` per project, screenshots restored afterwards):
  Debug 67/75 before the pc-commands update (that smoke then passed alone), Release 68/75. The 7
  failures are the known pre-existing ones: `PluginAssemblyIdentityProbe` and `NativeRuntimeProbe`
  (need arguments), hide-smoke-command (old command list), type-code-format (editor preview),
  pc-curve-display-colors ("must ship as package 1.0.73"), curve-release and surface-release
  (1.0.99 ID separators; "Role CID must remain unchanged").
- `git diff --check`: clean apart from line-ending notices.
- Package `PanelCladdingEditor-1.2.0-win-x64.zip`, SHA-256
  `8a91e3df6b0683e41fd72506386ccafff0b8b9a48e5b0105300c72c4fe79d8a7`; RHP SHA-256
  `30a914d1ce79f5fa0489164e13d440e313e48feeef9a935f159b94311f1d7e44`, assembly 1.2.0.0, assembly and
  plug-in class GUID `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` = manifest (identity probe on the bundle
  RHP). `Test-Package.ps1`: checksum, 32 payload hashes, isolated non-admin lifecycle with exactly
  thirteen commands. Staged at `C:\01_Projects\Panel Cladding Full\PanelCladdingEditor-1.2.0`.

## XYZ sheet

`C:\Projects\XYZ\Wireframe\XYZ - Naming Convention.xlsx`.
Snapshots are in the session scratchpad (16:12 and 16:40 versions; the 16:40 save by Excel only moved
the selection K24→K34). Before writing: Rhino and the workbook closed, file hash
`c9860222…b122` equal to the snapshot. A working copy was configured with `A`, `A`, `01`, `001`,
`01`, then the real file (temp copy + one-step move), at 16:59:34.

- Inspect: written codes Wall-Type A..N (13), Frame-Config A, Frame-Type 01–05 (with values),
  Cladding 001–035, Definition 01–64 (61 with values); no misfits.
- Changes (openpyxl comparison of value, type, strike, colour, bold, fill, number format, alignment,
  border for every cell): exactly 34 new cells, H26–H36 `P`..`Z` and J14–J36 `B`..`Z` (no I/O),
  plus the custom property. 0 cells changed in L–R; merges and column widths equal; J13 `A` =
  `0H 0V`; I25 stays the number 5. The real file equals the working copy cell for cell.
  Open XML SDK validation: 0 errors before and after. Result hash `4339b153…5b3d`.
- The save-time sync with the STN usage (F/L; A; 1A.1, 1A.2, 1C.1, 1C.2, 1C.4, 3A.2) on the configured
  copy: `PANEL_NAMING_CONVENTION_UNCHANGED`, bytes equal.

## Installation (problem and correction)

Pre-install read: `PlugIn\FileName` = `…\plugin\1.1.4\PanelCladdingEditor.rhp` (SHA-256 `e93dea9e…`),
12 commands, root 18:52:47Z, PlugIn 04:27:26Z, CommandList 04:36:02Z.

Deviation: after the user agreed, the agent ran the staged installer from its own tool shell. It
reported success and the agent's read-back (also with the tool sandbox disabled) showed 1.2.0 and 13
commands, but Rhino then reported the commands unknown. Read from inside Rhino, the real HKCU key still
pointed to `plugin\1.1.4\…` with 12 commands while the 1.1.4 folder had been moved to rollback
(`rollback\5615f295…\prior-plugin-1.1.4`). The agent's shell sees a private, virtualized HKCU (file
writes are real, registry writes are not), so its installer run and read-back were not host evidence
— exactly what the AGENTS.md attestation rule forbids relying on. The prior RHP was not reachable
for about 10 minutes; no model was saved in that time with the plug-in loaded.

Correction: the user closed Rhino and ran the staged `Install.cmd` from Explorer (Install + Validate
passed). A read-back script started by the user (`PanelCladdingEditor-attest\Read-Registration.cmd`,
host process) at 21:10:27Z: `FileName` = `…\plugin\1.2.0\PanelCladdingEditor.rhp`, SHA-256
`30a914d1…7e44`, exactly 13 commands (`2;<name>`), root/PlugIn/CommandList 21:10:05Z (all advanced).
The previous 1.2.0 folder went to `rollback\79cfa1e9…\prior-active`.

After the user started Rhino (PID 3564) and opened `XYZ - Wireframe_STN.3dm`: inside Rhino
`PlugInExists` = loaded, the only `PanelCladdingEditor` assembly is 1.2.0.0 from the installed path;
a read-back started by Rhino (host child process) at 21:13:56Z shows root and CommandList advanced
to 21:12:24Z and PlugIn unchanged at 21:10:05Z.

## Live acceptance (XYZ STN, 1.2.0)

- The user ran `PCtemplatePNL`, answered `A`, `A`, `01`, `001`: "already uses … nothing changed"
  (command history), and the document string `PanelCladdingEditor.PNLFormat` holds the format.
- The user saved the model (17:13:09); the save hook printed nothing and the sheet stayed
  byte-for-byte unchanged (hash `4339b153…5b3d`, time 16:59:34).
- All 96 panels under `XYZ-IEF-Wireframe::Surfaces - PNL` keep `CW_1.08_FRAME_CONFIG` = `A` and
  `CW_9.02_FRAME_CONFIG` = `0H 0V`.

## Acceptance alignment

PCtemplatePNL prompts, validation and re-prompting, example row, placeholders and the workbook
format record are implemented and covered; the sync follows the recorded format, keeps codes in
place, keeps placeholders and numbers and leaves non-sync columns alone. XYZ: format recorded, J13 `A`
with B..Z, wall placeholders to Z, L–R unchanged, STN save unchanged, panels keep `A`.

`XYZ - Wireframe.3dm` was not checked live (the user said it is not their latest work). An offline
read of part of it showed panels with blank unit type and frame config and wall types such as
`3A.5` that the sheet does not list; saving it with 1.2.0 would add wall code `P` = `3A.5` and set
the struck state from that model's usage. The struck state follows whichever model is saved.

## Rollback

Source: revert the branch. Plug-in: `rollback\5615f295…\prior-plugin-1.1.4` holds 1.1.4 with its
registration export; reinstalling the 1.1.4 package restores it. Sheet: the 16:40 snapshot
(`c9860222…b122`) can be put back; the placeholders and property are the only differences.

## Remaining limitations

- The agent's shell cannot read the host registry; attestation needs a user-started or Rhino-started
  process (`PanelCladdingEditor-attest\Read-Registration.cmd` stays for that).
- The struck state is per saved model; projects with several models sharing one sheet will see it
  change with each model's usage.
- Seq width is not part of the format (fixed four digits).
- No commit or PR was requested.

## Conclusion

1.2.0 is installed and attested on the host, PCtemplatePNL works in Rhino, and the XYZ sheet is
configured for `A`, `A`, `01`, `001`, `01`; saving the STN model leaves it unchanged.

## GitHub publication follow-up (2026-10-27)

Source commit: `971f652e7565c083af77ce55dac70001e3f2b5cf`.
Pull request: [#2 — project layer standard, ID separators and PNL naming convention](https://github.com/novan-xu/PanelCladding/pull/2).
