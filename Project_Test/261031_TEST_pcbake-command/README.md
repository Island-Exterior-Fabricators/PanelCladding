# PCbake command TEST

Evidence for [the PLAN](../../Project_Plan/261031_PLAN_pcbake-command.md) and
[the EXET](../../Project_Exet/261031_EXET_pcbake-command.md). The new rerunnable check is
[`tests/Smoke/pcbake-command`](../../tests/Smoke/pcbake-command/Program.cs).

| File | Contents |
| --- | --- |
| `regression-Debug.json`, `regression-Release.json` | `tests/Run-Regression.ps1` after the change: 78/78 each, exit 0. |

## Commands

```powershell
dotnet build .\PanelCladding.sln -c Debug -m:1
dotnet build .\PanelCladding.sln -c Release -m:1
.\tests\Run-Regression.ps1 -Configuration Debug -Name pcbake-command,panel-cladding-update-command,panel-cladding-pc-commands
.\tests\Run-Regression.ps1                                   # every smoke, Debug and Release
.\tests\Package\Verify-PluginAssemblyIdentity.ps1 -Configuration Release
```

## What `pcbake-command` checks

- `Surfaces` is the first (default) scope; `Includes` maps each scope to its object kinds.
- `Surfaces` scope: surfaces are created, rebuilt and deleted; no curve is created, rebuilt or
  deleted (including a stale curve); no kept-curve report.
- `Curves` scope: a panel without curves gets them; stale surfaces are left alone.
- `All` scope: a panel that owns any curve gets no new curve (not even a missing one), none of its
  curves (current or stale) is rebuilt or deleted, and it is reported; a panel without curves gets
  them; surfaces are reconciled as before. A curve without an owning panel marks nothing.
- Command source: `PCbake`, the former update GUID, `Scope` built from the enum, a local
  `scopeIndex = 0` (no static state), the preselection pass, material colours read only when
  surfaces are baked; the three removed command files are gone.
- Live source: existing dependencies are read before generation, curve planning is skipped for kept
  panels, existing dependencies record their owning panel, and the plan gets the scope.

## Updated smokes

`panel-cladding-update-command` (name, GUID, 3-argument contract, kept curves, installer list),
`panel-cladding-pc-commands` (11 commands, removed names rejected in source and installer),
`panel-cladding-split-spawn-sync` (removed command types absent, scope order),
`panel-cladding-spawn` (generator has no batch entry), `panel-cladding-managed-cid-scope` (bake
discovery filters instead of the removed spawn conflict filter), `pc-curve-display-colors`,
`panel-cladding-clear`, `panel-cladding-match`, `panel-cladding-surface-sync` (GUID uniqueness uses
the bake command), `panel-cladding-point-order` and `pcupdate-command-undo` (reflective `Apply`
takes the scope), and labels in `cladding-merge-mark-key`, `panel-cladding-full-track-collapse`,
`panel-cladding-hide-mask`, `pcspawnsrf-boundary-parent-order`, `pcupdate-locked-managed-objects`.
