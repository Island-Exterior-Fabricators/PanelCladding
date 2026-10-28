# Panel auto attributes TEST

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-auto-attributes`](../../tests/Smoke/panel-auto-attributes/) (run `.\tests\Run-Regression.ps1 -Name panel-auto-attributes`). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

Regression for [the PLAN](../../Project_Plan/261027_PLAN_panel-auto-attributes.md): the plug-in
keeps unit dimension, width, height, area and weight current on every `Surfaces - PNL` panel,
replacing the AutoATT script and its `AUTO_panel=1` mark.

## Managed smoke (`PanelAutoAttributesSmoke.csproj`)

```bash
dotnet run --project Project_Test/261027_TEST_panel-auto-attributes/PanelAutoAttributesSmoke.csproj -c Debug
```

Checks:

- the user's example (90 × 305 in, `CW_6.01_PSF` = 85) gives exactly
  `90.00000x305.00000`, `90.00000`, `305.00000`, `190.625 sqf`, `16203.125`; the same panel in a
  millimetre model keeps millimetre dimensions and converts the area to square feet; area and
  weight use three decimals and the weight multiplies the unrounded area;
- PSF is the leading number of `CW_6.01` (`85`, `85 psf`, `12.5PSF`, `.5`, `0`); blank, `TBD`,
  `psf 85` or `-` give the persisted blank weight;
- only stale or missing values are written, case variants are rewritten under the canonical key,
  a missing or blank weight satisfies a blank weight, and a stale weight is blanked when the PSF
  goes away; `AUTO_panel` is ignored;
- attribute edits re-queue a panel only for `CW_6.01` and the refreshed keys.

Measurement (`MeasurePanelExtent`, `AreaMassProperties`), scope and the event/idle wiring run inside
Rhino and are covered by the live checks below.

## Live acceptance (Rhino, not run by the agent)

Needs an installed 1.2.3 and a project model (for example `XYZ - Wireframe.3dm`):

1. Open the model: one line `unit dimension, area and weight refreshed on N panel(s)` when values
   changed; panels without `CW_2.07` now have it.
2. Scale or stretch a panel: `CW_2.00`–`CW_2.02`, `CW_2.07` and `CW_6.02` update after the command.
   `Undo` restores the previous geometry and values.
3. Copy a panel, and move an object from another layer onto a `Surfaces - PNL` sublayer: both get
   values. Objects outside the branch are untouched.
4. Edit `CW_6.01_PSF` in the Properties panel: `CW_6.02` updates; clear it: `CW_6.02` becomes blank.
5. `PCSyncSrf` on refreshed panels reports no grid change caused by the dimensions.
