# Panel cladding naming-convention TEST

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-naming-convention`](../../tests/Smoke/panel-cladding-naming-convention/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-naming-convention`). Commands below are historical; see the [regression test layout EXET](../../Project_Archive/Project_Exet/261028_EXET_regression-test-layout.md).

Plan: [261023_PLAN_panel-cladding-naming-convention](../../Project_Plan/261023_PLAN_panel-cladding-naming-convention.md)

| Attribute | Before 1.1.0 | Since 1.1.0 |
| --- | --- | --- |
| Cladding logic | `CW_2.13_CLADDING_LOGIC` | `CW_9.01_CLADDING_LOGIC` |
| Frame config | JSON masks in `CW_1.08_FRAME_CONFIG` | code `AA` in `CW_1.08`, text `1H 1V \| delete A1` in `CW_9.02_FRAME_CONFIG` |
| Unit type | `flat`, `corner_parent`, `corner_child` | `C`, `F`, `L`, `Z` |
| Role | from the unit type | `CW_5.00_DEFINITION_A`: `Parent`, `Child`, `Child 1`, … |

The smoke checks:

- the readable format (exact text, lossless round trip, hyphen ranges, rejected text) and
  that planners write only `CW_9.02`, with the derived code ignored by change detection;
- JSON configs converting to the text, coded panels reading `CW_9.02`, grid mismatch and
  missing-backup errors, and the `CW_9.01` / retired `CW_2.13` keys;
- code assignment: same intermediate counts share the first letter, `I`/`O` skipped,
  stable codes after a JSON round trip, conflicts rejected;
- CIDs for C/F (no suffix) and L/Z (`_P`, `_C`, `_C1`, `_C2`), stale and missing roles;
- Definition A reservation (shift to E, flat/curved always Parent, idempotent);
- PNL tables (used flags, reserved sheet-only rows, dropped foreign rows, wall-type codes);
- the workbook writer (1.1.1 layout) on generated workbooks: tables follow their row-12
  titles (moved titles, case/space/hyphen-insensitive matching, missing titles added in
  E/H/J, overlapping or duplicate titles rejected); only the six managed columns from row 13
  are rewritten, so header rows, another table (Cladding), unrelated merges and other sheets
  stay; row styles come from the table's own rows, else the title without bold; unused rows
  struck through in grey; schema-valid output; no rewrite when nothing changed; no style
  growth; a locked file left unchanged; no temporary or backup files left.

Since 1.2.0 (PCtemplatePNL, [261026 PLAN](../../Project_Archive/Project_Plan/261026_PLAN_pctemplatepnl-naming-format.md))
the smoke also checks the PNL format tokens and sequences, frame-config codes per format, and
`Program.Template.cs`: the template on XYZ-like and ABC-like sheets (placeholders only, segment
widening/narrowing with cells, merges and widths, refusals) and the save-time sync leaving a
configured sheet byte-for-byte unchanged. The workbook checks now expect in-place reconciliation:
codes keep their rows, placeholders are filled, foreign rows, merges and numbers are kept.

`VerifyMainModel` ([261026 PLAN](../../Project_Plan/261026_PLAN_naming-sync-main-model.md)) checks
that only `<PROJECT> - Wireframe.3dm` counts as the main model that writes the sheet on save:
`XYZ - Wireframe_STN.3dm`, versions, copies, other extensions and code-less names do not.

```powershell
dotnet run --project Project_Test/261023_TEST_panel-cladding-naming-convention/PanelCladdingNamingConventionSmoke.csproj -c Debug
dotnet run --project Project_Test/261023_TEST_panel-cladding-naming-convention/PanelCladdingNamingConventionSmoke.csproj -c Release
```

No Rhino native runtime is required. The save hook (`LivePanelNamingConventionSync`), the
per-document code table and the ABC migration run inside Rhino and are recorded in the EXET.

Since 1.2.2 these keys are `UD_CLADDING_LOGIC` and `UD_FRAME_CONFIG` (the smoke asserts the new
cladding-logic name); see [the UD_ key TEST](../261027_TEST_panel-cladding-ud-keys/README.md).
