# Panel cladding attribute conventions TEST

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-attribute-conventions`](../../../tests/Smoke/panel-cladding-attribute-conventions/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-attribute-conventions`). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

Regression for [the PLAN](../../Project_Plan/261027_PLAN_panel-cladding-attribute-conventions.md):
the plug-in follows the attribute conventions of the current XYZ and ABC wireframe models (1.2.5).

| Content | 1.2.4 | 1.2.5 |
| --- | --- | --- |
| Panel frame | `Plane` (geometry user string), else face plane | `UD_PLANE` (object user text), an auto value |
| Frame/extrusion assignments | `CW_1.09_FRAME_TYPE` | `UD_FRAME_ASSIGNMENTS` |
| `CW_1.09_FRAME_TYPE` | payload; deleted by `PCCreate` | hand-filled; recorded in the PNL Frame-Type table |
| PCpid setup keys | existing value copied back, case variants consolidated | written only when missing |
| Role (`CW_5.00_DEFINITION_A`) | `Parent`, `Child` | `parent`, `child` |
| Object name | drops `CID-ABC-` (other projects only `CID-`) | drops `CID-<project>-` |

## Managed smoke (`PanelCladdingAttributeConventionsSmoke.csproj`)

```bash
dotnet run --project Project_Test/261027_TEST_panel-cladding-attribute-conventions/PanelCladdingAttributeConventionsSmoke.csproj -c Debug
```

Checks:

- key names; `PCClear` removes `UD_FRAME_ASSIGNMENTS` but not `CW_1.09_FRAME_TYPE`;
- the assignment payload decodes from `UD_FRAME_ASSIGNMENTS` (legacy `CW_2.09` still reads); a
  payload or text in `CW_1.09` is never decoded; a panel with XYZ's `oCornerR` (and a case variant
  with another value) parses and validates; conflicting `UD_FRAME_ASSIGNMENTS` variants still fail;
- `UD_PLANE` keeps ABC's form, parses and unitizes axes, rounds to six decimals without negative
  zero, and matches with tolerance (format-only differences are current; a moved origin, flipped
  axis, blank or degenerate value is not);
- `UD_PLANE` is an auto key and input key; the change set keeps an equivalent value, refreshes a
  stale origin, adds `UD_PLANE` next to a legacy `Plane` (left alone) and rewrites a case variant;
- the Frame-Type table: sheet codes kept, a new value takes the next code, `NA` is used while a
  frame type is blank, values match ignoring case, natural ordering, an exhausted format reports the
  value;
- the PNL workbook: ABC's layout (row-7 header only) gets no table while nothing is recorded and a
  titled table under the header once a value exists (other tables unchanged, reads back); XYZ's
  titled table fills its first placeholder row, keeps `NA`/used rows plain, is not rewritten when
  current, and strikes rows that become unused without changing them;
- `PCpid` writes no existing key (any case, blank or not), adds blanks only for missing setup keys,
  never adds `CW_1.12`/`CW_1.10_REVIT_TYPE`, and still writes identity, dimensions and `parent`;
- lowercase roles from `PCpid` and `NormalizeRole`; role suffixes read any case;
- object names for XYZ, ABC, mixed case, component suffixes, a `CID-` value without a project
  segment and a custom name.

Older smokes updated for the new keys are listed in the EXET.

## Live acceptance (Rhino, not run by the agent)

Needs an installed 1.2.5:

1. Open `ABC - Wireframe.3dm`: the open summary reports the refreshed panels; a panel's `UD_PLANE`
   keeps its axes (for example `{1,0,0};{0,0,1}` on E1) with the lower-left corner as origin;
   panels without `UD_PLANE` gain one. `PCEditor` layouts are not mirrored.
2. Open `XYZ - Wireframe.3dm`: all 401 panels gain `UD_PLANE`; a corner panel (`CW_1.09_FRAME_TYPE`
   `oCornerL`) opens in `PCEditor`.
3. Run `PCCreate` on a XYZ corner panel: `CW_1.09_FRAME_TYPE` keeps its value.
4. Save the main XYZ model: the PNL Frame-Type table keeps `01`-`05` and strikes nothing in use.
5. Run `PCpid` on a panel with a hand-filled `CW_2.06_PENETRATION_DIMENSION`: the value stays; a ABC
   panel's `Parent` becomes `parent` on its next cladding write.
