# PCweb command EXET

## Corresponding plan and execution date

Executed 2026-11-01 under [the user-approved PLAN](../Project_Plan/261101_PLAN_pcweb-command.md).

## Related artifacts

[TEST evidence](../Project_Test/261101_TEST_pcweb-command/README.md). Branch `codex/pcweb` from
`master` (`06a7cbc`), worktree `C:\01_Projects\Panel Cladding Full\PC-pcweb`;
[PR 16](https://github.com/nuofan-island/PanelCladding/pull/16). The launch contract is
Wireframe Web 1.0.0's `release/windows/README.md` (Wireframe Web repository, `f5ec355`).

## Implemented scope

- `Infrastructure/WireframeWeb/WireframeWebLauncher.cs` (no RhinoCommon):
  - `FindLauncher`: registry `HKCU\Software\IEF\Wireframe Web` `Launcher`, else
    `%LOCALAPPDATA%\Programs\Wireframe Web\wireframe-web.cmd`, else null; `NotInstalledMessage` names
    the release page and `Install Wireframe Web.cmd`.
  - `ProjectCode`: the code of a `<CODE> - ....3dm` name (the plug-in's `CodeFromDocumentPath`,
    requiring the `" - "` and `.3dm`), else empty.
  - `Open(launcher, model, timeout)`: starts `cmd.exe /d /s /c ""<launcher>" --open "<model>"
    1>"<out>" 2>"<err>""` through the shell with a hidden window, waits up to the timeout (3 minutes
    from the command) and returns the last stdout line on exit 0, else the stderr text and exit
    code. Output files live in `%TEMP%\PanelCladdingEditor\PCweb`; files older than a day are removed.
- `UI/PanelCladdingWebCommand.cs`: `PCweb` (command GUID `A073B617-D8ED-4689-8B3E-3B05B6B9F7E8`).
  Checks path, name and launcher; a run while one waits says "still opening"; a modified model is
  saved (Yes), left (No) or the command stops (Cancel), `-PCweb` offers `Save` / `LastSave`; a
  modified read-only model opens its last save without a question. The launch runs in `Task.Run`;
  its result is written through `RhinoApp.InvokeOnUiThread`.
- Installer `CommandList` and the registration checks: 13 commands (`PCweb` added).
- Smokes: new `pcweb-command`; `panel-cladding-pc-commands` expects `PanelCladdingWebCommand` /
  `PCweb`.
- `tests/Live/PcLive.cs`: `web` suite (W.0-W.4) with an `assembly` parameter for a build that is not
  installed; `commands` expects the 13 names. `tests/Live/SaveProbe.cs`: the save probe used for
  the finding below. Package README and `tests/Live/README.md` describe PCweb and the suite.

## Deviations

- **Launcher start (contract change).** The contract's `ProcessStartInfo` (`UseShellExecute=false`
  with redirected stdout/stderr) makes the launcher inherit Rhino's inheritable handles, Rhino's
  lock on the open model among them. The launcher starts Edge, which can outlive it, and while such a
  process runs `RhinoDoc.Save()` fails with Rhino's "File Is Read-only" message. The live suite hit
  this at W.3 twice; the save probe reproduced it with a plain `cmd /c ping` child and showed that a
  shell-started child does not cause it (TEST, "Finding"). PCweb starts `cmd.exe` through the shell
  and reads the output from files instead. Arguments, exit codes and output meaning are unchanged.
- **Read-only models.** Not in the PLAN: a modified model Rhino opened read-only is not asked about
  (Yes could only end in Rhino's read-only message); its last save opens, with a line saying so.
- **Live acceptance against an uninstalled build.** The installed plug-in is 1.3.0, so the `web`
  suite loads this branch's Release RHP into a separate load context and calls
  `PanelCladdingWebCommand.Open` with the answer given directly. Command registration, the
  Yes/No/Cancel dialog and the `-PCweb` prompt are covered by the smokes and the registration checks,
  not live; they follow once a package with PCweb is installed.
- The version stays 1.3.0 (the release is paused).

## Discovered issues

- The Wireframe Web contract's start code has the handle problem above for any caller that keeps a
  file open; its own `pcweb-check.ps1` runs from PowerShell and does not show it. Reported to the
  Wireframe Web session, which changed its contract text and `pcweb-check.ps1` to the shell start
  (not committed there yet; the 1.0.0 zip's README keeps the old snippet). The launcher is unchanged,
  so PCweb works with 1.0.0.
- Wireframe Web has no endpoint to delete a project, so the test project `PCW` stays in this PC's
  Wireframe Web data.
- 2026-11-01 is the EDT -> EST change, so local times in the evidence repeat the 01:00 hour.

## Commands and results

| Command | Result |
| --- | --- |
| `dotnet build ... -c Debug` / `-c Release` (production RHP) | 0 warnings, 0 errors |
| `Verify-PluginAssemblyIdentity.ps1 -Configuration Debug` / `Release` | one plug-in id, `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` |
| `Run-Regression.ps1` (first build) | Debug 82/82, Release 82/82 |
| `Run-Regression.ps1 -Configuration Debug -Name pcweb-command` (final build) | passed |
| `Run-Regression.ps1` (final build) | Debug 82/82, Release 82/82 |
| Live `web` suite, final build | 13/13 |
| Live read-only check | passed |

## Acceptance alignment

- Managed acceptance in the PLAN: met (launcher lookup, name check, fake launcher in a folder with
  spaces receiving a model path with spaces and `&`, exits 0/1/2/3, timeout, a child holding the
  output, unstartable launcher, source order of checks / question / worker-thread launch / UI-thread
  result, and the launcher started without Rhino's handles).
- `panel-cladding-pc-commands` and the package registration checks expect 13 commands: met.
- Live: saved model, LastSave, Save, Cancel and a second run while one waits: met in Rhino with
  Wireframe Web 1.0.0, against the uninstalled build as described under Deviations.

## Rollback

Revert the branch; Wireframe Web is unaffected. Delete `%TEMP%\PanelCladdingEditor\PCweb` if wanted.

## Remaining limitations

- Live checks of the installed command (registration, dialog, `-PCweb`) wait for a package with
  PCweb.
- A model path with `%` may be expanded by `cmd.exe`.

## Conclusion

PCweb opens the active saved model in Wireframe Web, leaves Rhino responsive while it starts, and
leaves the model saveable afterwards. Not packaged, installed or released.
