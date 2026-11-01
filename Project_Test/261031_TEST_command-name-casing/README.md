# Command name casing TEST

Evidence for [the PLAN](../../Project_Plan/261031_PLAN_command-name-casing.md) and
[the EXET](../../Project_Exet/261031_EXET_command-name-casing.md). The rerunnable check is
[`tests/Smoke/panel-cladding-pc-commands`](../../tests/Smoke/panel-cladding-pc-commands/Program.cs).

| File | Contents |
| --- | --- |
| `regression-Debug.json`, `regression-Release.json` | `tests/Run-Regression.ps1` after the rename: 77/77 each, exit 0. |
| `registry-recreate-probe.txt` | How the installer's `CommandList` write behaves on an upgrade. |

## Commands

```powershell
dotnet build .\PanelCladding.sln -c Debug -m:1
dotnet build .\PanelCladding.sln -c Release -m:1
.\tests\Run-Regression.ps1 -Configuration Debug -Name panel-cladding-pc-commands
.\tests\Run-Regression.ps1                                   # every smoke, Debug and Release
git grep -n -E "(^|[^A-Za-z0-9])PC(Clear|Create|CrvTemplate|Editor|MatchCrv|MatchSrf|SpawnCrv|SpawnSrf|SyncCrv|SyncSrf|Update)\b" -- . ":!Project_Archive" ":!Project_Exet" ":!Project_Plan" ":!Project_Test" ":!Packaging/PanelCladdingEditor/RELEASE-NOTES-*"
```

## What `panel-cladding-pc-commands` checks

- The 13 command types and their `EnglishName` values: `PCeditor`, `PCcreate`, `PCclear`,
  `PCcrvTemplate`, `PCmatchSrf`, `PCmatchCrv`, `PCspawnCrv`, `PCspawnSrf`, `PCsyncCrv`,
  `PCsyncSrf`, `PCupdate`, `PCpid`, `PCtemplatePNL`; GUIDs explicit and unique.
- Every name matches `^PC[a-z]+(?:[A-Z][a-z]+|PNL)*$`.
- `src/PanelCladdingEditor` (`.cs`, `.xaml`), the installer, `Install-Package.ps1`,
  `QUICKSTART.txt` and `package-manifest.json` contain no former spelling.
- `package-manifest.json` names `PCeditor` as the package command.
