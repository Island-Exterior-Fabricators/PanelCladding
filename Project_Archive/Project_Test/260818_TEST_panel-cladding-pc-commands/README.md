# Panel cladding PC command-name smoke

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-pc-commands`](../../../tests/Smoke/panel-cladding-pc-commands/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-pc-commands`). Commands below are historical; see the [regression test layout EXET](../../../Project_Exet/261028_EXET_regression-test-layout.md).

This smoke locks the complete Rhino command surface to the shortened `PC` prefix while preserving
the `PanelCladdingEditor` plug-in identity and every command class/GUID.

Expected commands:

- `PCEditor`
- `PCCreate`
- `PCClear`
- `PCCrvTemplate`
- `PCMatchSrf`
- `PCMatchCrv`
- `PCSpawn`
- `PCSync`

Run both configurations from the repository root:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-pc-commands\PanelCladdingPcCommandsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-pc-commands\PanelCladdingPcCommandsSmoke.csproj -c Release
```
