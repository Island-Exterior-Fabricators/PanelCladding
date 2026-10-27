# Panel cladding logic authority TEST

Regression for [the PLAN](../../Project_Plan/261027_PLAN_panel-cladding-logic-authority.md):
in 1.2.4 `UD_CLADDING_LOGIC` becomes the only cladding the plug-in reads, `CW_4.xx` is output only,
and `PCCreate` no longer generates or overwrites cladding.

| Panel state | Load depends on | `PCCreate` |
| --- | --- | --- |
| Unconfigured (no `UD_CLADDING_LOGIC`) | H/V offsets (frame configuration, which `PCCreate` writes, must fit them) | writes the grid only; `CW_4` untouched |
| Configured | H/V, `UD_CLADDING_LOGIC` cell set, `UD_FRAME_CONFIG` | keeps the logic if it fits the new grid, deletes it otherwise |

## Managed smoke (`PanelCladdingLogicAuthoritySmoke.csproj`)

```bash
dotnet run --project Project_Test/261027_TEST_panel-cladding-logic-authority/PanelCladdingLogicAuthoritySmoke.csproj -c Debug
```

Checks:

- logic format: owner cells store the trimmed material (readable `&`), members their owner label,
  unassigned cells `""`; `DecodeValues` keeps materials, `Decode` reads them as roots, owner
  resolution still works; ownership-only logic is recognized; unknown parents and cycles fail;
- unconfigured panel: extra, missing and invalid `CW_4` keys neither fail nor fill the parse;
  a blank logic counts as unconfigured;
- configured panel: cell values come from the logic, not `CW_4`; an extra V offset or a short
  logic fails `PANEL_CLADDING_LOGIC_GRID_MISMATCH`; a frame configuration of another size fails
  `PANEL_FRAME_CONFIG_GRID_MISMATCH`; ownership-only `CW_9.01` logic takes root materials from
  `CW_4` while parents still come from the logic;
- `PCCreate` plan: no `CW_4` or logic writes; user `CW_4` keys are not deleted; old offsets,
  `CW_2.13_CLADDING_LOGIC` and retired type keys are; logic that fits the new grid is kept; stale
  (`UD_` in any case and `CW_9.01`) or unreadable logic is deleted;
- `PCMatchSrf`: an unconfigured source fails `PANEL_CLADDING_MATCH_SOURCE_NOT_CONFIGURED`; a
  configured source transfers its logic (materials included) and writes `CW_4` from it, ignoring
  a stale source `CW_4` value;
- editor save: Save Extrusions that adds an H track to a configured panel fails
  `PANEL_CLADDING_LOGIC_REQUIRES_SAVE_BOTH`; the same save without a size change, or on an
  unconfigured panel, succeeds.

## Live acceptance (Rhino, not run by the agent)

Needs an installed 1.2.4:

1. `PCCreate` on a panel with hand-typed `CW_4` keys: they remain; no `UD_CLADDING_LOGIC` is added.
2. `PCEditor` on that panel opens with blank cells. Assign materials, Save Cladding: the panel has
   `UD_CLADDING_LOGIC` with the materials and matching `CW_4` keys.
3. Edit a `CW_4` value by hand and reopen: the editor still shows the logic's material.
4. Re-run `PCCreate` with the same guides: the cladding is kept. Add a guide and re-run: the logic
   is deleted and the editor opens blank. One `Undo` restores it.
5. A panel saved by 1.2.3 or earlier opens with its materials; after Save Cladding its logic
   carries them.
