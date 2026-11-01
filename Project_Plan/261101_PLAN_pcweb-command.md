# PCweb command PLAN

Execution date: 2026-11-01 (America/New_York).

## Background and authorization

Wireframe Web is the wireframe team's local, read-only web viewer for the saved Rhino wireframe
model. Version 1.0.0 is released as its own per-user install (no administrator rights) and does not
ship with this plug-in. The user asked for a command that opens the current project in Wireframe
Web when it is installed. They chose the name `PCweb` and approved this plan on 2026-11-01
("开始实现"). The version stays at 1.3.0 on this branch: the user paused the release
(`codex/release-1.3.1` is unchanged) and will decide later whether PCweb ships in 1.3.1 or 1.4.0.

The launch contract comes from the Wireframe Web 1.0.0 package
(`release/windows/README.md` in the Wireframe Web repository):

- `HKEY_CURRENT_USER\Software\IEF\Wireframe Web`, string `Launcher` = full path of
  `wireframe-web.cmd` (also `InstallDir`, `Version`, `Port`). Default install:
  `%LOCALAPPDATA%\Programs\Wireframe Web\wireframe-web.cmd`.
- `"<Launcher>" --open "<model>.3dm"`, no window. It starts Wireframe Web (tray icon and server) or
  reuses the running one, loads the file's latest save and opens a browser tab on the project.
- Exit 0: the page address is the last line of stdout. Exit 1: no such file or no project code;
  2: the server did not start; 3: the model failed to load. One-line message on stderr otherwise.
- First start takes up to about 60 s; later calls take seconds.
- The model's file name must be `<CODE> - ….3dm`; Wireframe Web reads the saved file.

This request authorizes the command, its tests and PLAN/EXET/TEST records; not packaging,
installation, a version change or a release.

## Goals

`PCweb` opens the active saved model in Wireframe Web:

1. Without a saved path: "save first" and stop. A file name without `<CODE> - ` before `.3dm`:
   say so and stop, without starting Wireframe Web.
2. Find the launcher: registry `Launcher`, else the default install path. When neither exists, name
   the download page and `Install Wireframe Web.cmd`, and stop.
3. Unsaved changes: interactive runs ask Yes / No / Cancel ("Save changes to <file> before opening
   it in Wireframe Web?"): Yes saves then opens, No opens the last save, Cancel stops. Scripted runs
   (`-PCweb`) offer the command-line options `Save` (Enter) and `LastSave`.
4. Run the launcher with no window and wait off the UI thread. The command ends at once with
   "opening <CODE> in Wireframe Web..."; the result follows on the command line: the page address,
   or Wireframe Web's message and exit code. A run while the previous one is still waiting says so
   and starts nothing. After 3 minutes without an exit the command stops waiting and says so; the
   launcher is left running.
5. Wireframe Web keeps running after Rhino closes. The plug-in depends only on the registry value
   and the launcher's arguments, output and exit codes; it uses no Wireframe Web code, port or API.

## Architecture ownership

- Infrastructure: `Infrastructure/WireframeWeb/WireframeWebLauncher.cs` (no RhinoCommon) finds the
  launcher, checks the model file name, runs the launcher and turns its exit code and output into a
  result.
- UI: `UI/PanelCladdingWebCommand.cs` (`PCweb`, new command GUID) checks the document, asks about
  unsaved changes, starts the launch on a worker thread and writes the result on the UI thread.
- Packaging: the installer's `CommandList` gains `PCweb` (13 commands).

## Affected files

New `WireframeWebLauncher.cs`, `PanelCladdingWebCommand.cs`; `Install-PanelCladdingEditor.ps1`
(expected commands); `tests/Package/Test-PanelCladdingPluginRegistration.ps1` and
`Test-UserInstallation.ps1` (13 commands); smokes `panel-cladding-pc-commands` (command set) and
new `pcweb-command`; `tests/Live/PcLive.cs` (13 commands; new `web` suite) and its README; package
README.

## Usage

Save the model as `<CODE> - Wireframe.3dm` (any `<CODE> - ….3dm` works), run `PCweb`. The browser
opens on the project; use Refresh model in the page after later saves, or run `PCweb` again.

## Acceptance

- Managed (`pcweb-command`): launcher lookup (registry value, fallback, missing); file-name check;
  a fake launcher in a folder with spaces receives `--open "<model>"` for a model path with spaces
  and `&`; exit 0 returns the last stdout line as the address; exits 1/2/3 return the stderr message
  and code; a launcher that outlives the timeout is reported; a launcher whose child keeps its
  output open still returns promptly with its output. Source checks: the command checks the path,
  asks before launching a modified document, launches off the UI thread and writes on it.
- `panel-cladding-pc-commands` and the package registration checks expect 13 commands.
- Debug and Release builds with zero warnings; the full regression passes in both.
- Live (`tests/Live`, `web` suite) against the plug-in built from this branch in Rhino on the MET
  test model with Wireframe Web 1.0.0 installed: `PCweb` reports the page address; the project's
  model in Wireframe Web is the saved file; a modified document with `-PCweb LastSave` opens the
  last save without saving; a second run while one waits starts nothing.

## Risks and rollback

The launcher runs through `cmd.exe`; a model path with `%` could be expanded by the launcher script.
Model folders do not use `%`. Revert the branch to remove the command; Wireframe Web is unaffected.

## Future extensions

A toolbar button; reading `Port` to reuse an open tab instead of opening a new one.
