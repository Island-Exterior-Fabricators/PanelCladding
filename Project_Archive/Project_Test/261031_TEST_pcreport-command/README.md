# PCreport command TEST

> **Client data removed on 2026-11-04.** Files this record produced from client project models (exports, screenshots, object and layer dumps) were removed from the repository and its history, and project codes and paths here were replaced with fictional ones; see the [client data cleanup EXET](../../../Project_Exet/261104_EXET_client-data-cleanup.md). The conclusions below stand; links to the removed files no longer resolve.

Evidence for [the PLAN](../../Project_Plan/261031_PLAN_pcreport-command.md) and
[the EXET](../../Project_Exet/261031_EXET_pcreport-command.md). The rerunnable check is
[`tests/Smoke/pcreport-command`](../../../tests/Smoke/pcreport-command/Program.cs).

| File | Contents |
| --- | --- |
| `pcreport-window-1280x800.png` | Off-screen render of the report window on the smoke fixture (rule "CID does not match PID and role" selected). |
| `regression-Debug.json`, `regression-Release.json` | `tests/Run-Regression.ps1` on this branch (includes `excluded-layer-keywords`). |

## Commands

```powershell
.\tests\Run-Regression.ps1 -Configuration Debug -Name pcreport-command,panel-cladding-pc-commands
.\tests\Run-Regression.ps1                                   # every smoke, Debug and Release
dotnet build .\PanelCladding.sln -c Debug -m:1
dotnet build .\PanelCladding.sln -c Release -m:1
.\tests\Package\Verify-PluginAssemblyIdentity.ps1 -Configuration Release
```

## What `pcreport-command` checks

- A ABC-like snapshot (`ABC - Wireframe.3dm`) with one compliant panel, surface and curve and one
  object per problem. Each of the 27 rules fires for its object or layer; the missing-root rule is
  checked on a second snapshot. The compliant panel, surface and curve raise nothing; a panel on
  `Surfaces - Not in Scope` is not checked and its surface is reported as having an untracked owner.
- Missing setup keys are Info; rule IDs are unique; counts are 8 panels, 6 managed surfaces and
  2 managed curves (the wireframe glass copy is not managed).
- Export writes `Summary` and `Issues` sheets, one issue row per issue under the fixed header, and
  leaves no temporary file.
- The window (constructed off-screen, never shown) displays the counts, 27 rules in 3 groups with
  the first selected, lists both duplicate-CID panels, passes selected object IDs to the navigator,
  ignores layer-level rows and shows "No issues" for an empty report.
- The report reader's source contains no object/layer write, user-string write or Undo record.
