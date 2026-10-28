# Panel Cladding Offset Sync Smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-offset-sync`](../../tests/Smoke/panel-cladding-offset-sync/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-offset-sync`). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

This folder owns focused regression coverage for geometry-derived H/V offset replacement and the
`04_STEP Surfaces` spawn root plus exact legacy-root read compatibility.

Offsets are canonicalized to five decimal places before grid validation. The smoke verifies
rounding, compact `0.#####` serialization, and fail-closed rejection when two boundaries collapse
to the same five-decimal coordinate.

The standalone smoke must cover canonical key replacement, changed-grid sync planning, STEP layer
acceptance, and rejection of both legacy material roots. A live Rhino fixture should additionally
verify naked-edge extraction and geometry coverage on planar and curved panels.
