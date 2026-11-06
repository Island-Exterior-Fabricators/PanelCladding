# Panel cladding UD_ user-defined keys TEST

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-ud-keys`](../../tests/Smoke/panel-cladding-ud-keys/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-ud-keys`). Commands below are historical; see the [regression test layout EXET](../../Project_Archive/Project_Exet/261028_EXET_regression-test-layout.md).

Regression for [the PLAN](../../Project_Plan/261027_PLAN_panel-cladding-ud-keys.md):
the `CW_9.xx` series becomes `UD_XXX` in 1.2.2.

| Content | 1.1.0–1.2.1 | 1.2.2 |
| --- | --- | --- |
| Cladding logic | `CW_9.01_CLADDING_LOGIC` | `UD_CLADDING_LOGIC` |
| Frame config | `CW_9.02_FRAME_CONFIG` | `UD_FRAME_CONFIG` |

## Managed smoke (`PanelCladdingUdKeysSmoke.csproj`)

```bash
dotnet run --project Project_Test/261027_TEST_panel-cladding-ud-keys/PanelCladdingUdKeysSmoke.csproj -c Debug
```

Checks:

- the key constants and each `UD_` key's legacy alias; planners write `UD_FRAME_CONFIG`;
- a panel carrying only `CW_9.01`/`CW_9.02` decodes, reads its canonical configuration, validates
  and reads its cladding logic; a `UD_` value wins over a leftover legacy value; a blank
  `UD_FRAME_CONFIG` falls back; a code without either backup still fails
  `PANEL_FRAME_CONFIG_BACKUP_MISSING`;
- the migration plan renames legacy keys in any case, keeps a newer `UD_` value (only deleting the
  legacy key), is empty for a migrated panel, drops a blank legacy key and leaves unrelated keys
  (`UD_OFFSET_CUSTOM`, `CW_9.99_OFFSET_CUSTOM`) alone;
- legacy keys are topology/clearable keys, a stored `CW_9.02` fails `TopologyPersistenceMatches`,
  and `PCClear` removes the legacy keys but not unrelated `UD_` or identity keys.

The naming-convention smoke (`261023_TEST_panel-cladding-naming-convention`) asserts the new
`CladdingLogicKey` name.

## Live acceptance (Rhino, not run by the agent)

Needs an installed 1.2.2 and a model written by 1.1.0–1.2.1 (ABC or XYZ):

1. `PCEditor` on a coded panel shows its saved layout (no `PANEL_FRAME_CONFIG_BACKUP_MISSING`).
2. Save it: the panel has `UD_CLADDING_LOGIC` and `UD_FRAME_CONFIG`, no `CW_9.01`/`CW_9.02`, and
   the same `CW_1.08` code. One `Undo` restores the `CW_9` keys.
3. Save the main model: the PNL sheet is unchanged apart from normal usage updates.
