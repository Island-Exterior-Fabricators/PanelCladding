# PCCreate command smoke test

> **Code moved on 2026-10-28.** This folder keeps the evidence only: the smoke project is in [`tests/Smoke/panel-cladding-create-command`](../../tests/Smoke/panel-cladding-create-command/) (run `.\tests\Run-Regression.ps1 -Name panel-cladding-create-command`). Commands below are historical; see the [regression test layout EXET](../../Project_Exet/261028_EXET_regression-test-layout.md).

This test validates the pure grid-construction plan and the Rhino command contract for
`PCCreate`. The command now persists segment/merge masks and only the surviving logical blank cells inferred from complete on-panel atomic guide spans.

Coverage includes:

- panel-local H/V guide classification;
- bottom-up H offsets and left-right V offsets;
- duplicate, remote, exterior, and diagonal guide handling;
- canonical offset and cell user-text keys;
- authoritative reset of prior grid/type/signature metadata;
- preservation of unrelated panel metadata;
- multi-panel planning and duplicate panel selection;
- failure-before-mutation conditions; and
- the exact panel-first, curve-second Rhino prompt sequence.

Run with:

```powershell
dotnet run --project Project_Test/260818_TEST_panel-cladding-create-command/PanelCladdingCreateSmoke.csproj -c Debug
dotnet run --project Project_Test/260818_TEST_panel-cladding-create-command/PanelCladdingCreateSmoke.csproj -c Release
```

Since 1.2.4 `PCCreate` writes no `CW_4` placeholders or logic and keeps existing `CW_4` keys; the smoke's expected writes and deletes were updated accordingly (see [the logic authority TEST](../261027_TEST_panel-cladding-logic-authority/README.md)). Results recorded above predate that change.
