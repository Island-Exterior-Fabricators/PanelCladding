# Panel cladding wheel-zoom aspect smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-wheel-zoom-aspect`](../../../tests/Smoke/panel-cladding-wheel-zoom-aspect/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-wheel-zoom-aspect`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

This focused WPF smoke reproduces and prevents the preview-stretch bug reported for panel
`E1_05_48` (`18.75 × 199.5`).

It verifies:

- the former independent 80-DIP width/60-DIP height clamps reproduce height-only growth;
- 80%, 100%, and 150% use the model aspect ratio;
- both axes receive the same zoom factor;
- positive and negative wheel detents change zoom by 5% and are handled;
- zero-pan centering remains stable;
- cladding hit rectangles and extrusion view share the corrected bounds; and
- deterministic 80%/150% preview renders.

Run with:

```powershell
dotnet run --project Project_Test/260818_TEST_panel-cladding-wheel-zoom-aspect/PanelCladdingWheelZoomAspectSmoke.csproj -c Debug
dotnet run --project Project_Test/260818_TEST_panel-cladding-wheel-zoom-aspect/PanelCladdingWheelZoomAspectSmoke.csproj -c Release
```
