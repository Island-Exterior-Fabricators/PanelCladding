# Command name casing PLAN

## Background and authorization

On 2026-10-31, while planning a compliance report command, the user asked to standardize
the command name casing first: `PC` stays uppercase, the rest is lowercase, and compound
names keep camel case (`PCspawnCrv`). Today the names mix three styles (`PCEditor`, `PCpid`,
`PCtemplatePNL`). This request authorizes the construction change and its PLAN/EXET/TEST
records. It does not authorize packaging, installation or UI automation.

## Goals

- Every command follows `PC` + lowercase first word + capitalized later words.
- Messages, documentation, the installer command list and the package checks use the new
  names. Typing an old name in Rhino keeps working (Rhino command names are case-insensitive).
- Command GUIDs, the plug-in GUID and the command count (13) are unchanged.

## Names

| Before | After |
| --- | --- |
| `PCEditor` | `PCeditor` |
| `PCCreate` | `PCcreate` |
| `PCClear` | `PCclear` |
| `PCUpdate` | `PCupdate` |
| `PCMatchSrf` | `PCmatchSrf` |
| `PCMatchCrv` | `PCmatchCrv` |
| `PCSpawnSrf` | `PCspawnSrf` |
| `PCSpawnCrv` | `PCspawnCrv` |
| `PCSyncSrf` | `PCsyncSrf` |
| `PCSyncCrv` | `PCsyncCrv` |
| `PCCrvTemplate` | `PCcrvTemplate` |
| `PCpid` | unchanged |
| `PCtemplatePNL` | unchanged (`PNL` is an acronym) |

## Architecture ownership

`UI/` command adapters own `EnglishName`. Application/Infrastructure messages that name a
command change only their text. `Packaging/PanelCladdingEditor/` owns the expected
`CommandList` and the package metadata `command`. No Domain, attribute key, layer or
registration-structure change. No MCP surface.

## Design

- Replace each old name where it stands for the command: `EnglishName`, user-facing messages,
  comments, package README, QUICKSTART, repository README, `Install-Package.ps1`,
  `Get-PanelExpectedCommands`, `package-manifest.json` `command`, and the package/registration
  checks and smokes that assert names or messages.
- Leave historical records unchanged: `Project_Plan`, `Project_Exet`, `Project_Test`,
  `Project_Archive` and published release notes (`RELEASE-NOTES-1.0.97.md`,
  `RELEASE-NOTES-1.2.5.md`). Lowercase folder names such as `tests/Smoke/pcupdate-command-undo`
  are capability names and stay.
- Upgrade: the installer recreates the `CommandList` key before writing names, and its
  validation compares names case-insensitively, so an upgraded 1.2.5 registration ends with
  the new names only. Rhino's own command lookup, aliases and toolbar macros are
  case-insensitive, so `_PCEditor` macros keep working.
- Version 1.3.0 (`PanelCladdingEditor.csproj`, `package-manifest.json`). The compliance report
  and the excluded-layer keywords planned next ship in the same release.

## Affected files

- `src/PanelCladdingEditor/UI/*Command.cs` (11 renamed commands) and messages in
  `PanelCladdingMatchPlanningService.cs`, `PanelCladdingCreatePlanningService.cs`,
  `PanelCladdingSurfaceSyncService.cs`, `PanelCladdingCidService.cs`, `PanelCladdingKeyService.cs`,
  `PanelCladdingModels.cs`, `LivePanelCladdingRepository.cs`, `LivePanelNamingConventionSync.cs`.
- `Packaging/PanelCladdingEditor/`: `Install-PanelCladdingEditor.ps1`, `Install-Package.ps1`,
  `package-manifest.json`, `README.md`, `QUICKSTART.txt`; repository `README.md`.
- `tests/Package/Test-PanelCladdingPluginRegistration.ps1`, `tests/Package/Test-UserInstallation.ps1`
  and the smokes that name the commands (including `panel-cladding-pc-commands`).
- New `Project_Test/261031_TEST_command-name-casing/` and the matching EXET.

## Usage

Run commands by their new names (`PCeditor`, `PCspawnCrv`, ...). Old spellings still run.

## Acceptance

- `panel-cladding-pc-commands` asserts the 13 new names, unchanged GUIDs and `PCeditor` as the
  package command.
- No active source, packaging, documentation or test file names an old spelling outside the
  historical records listed above.
- Installer expected list, registration and user-installation checks use the new names.
- Debug and Release builds pass; `tests/Run-Regression.ps1` passes for both configurations.

## Rollback

Revert the branch. Old and new spellings resolve to the same commands, so documents, aliases and
macros need no change either way.

## Future extensions

None planned for naming. Later commands follow the same rule (`PCreport`).
