# PCweb command TEST

Evidence for [the PLAN](../../Project_Plan/261101_PLAN_pcweb-command.md) and
[the EXET](../../Project_Exet/261101_EXET_pcweb-command.md): the `PCweb` command opens the active
saved model in Wireframe Web 1.0.0 through its launch command.

Machine NOVAN-HOME, 2026-11-01. Local times in the result files are EST; the clock went back from
02:00 EDT to 01:00 EST that night, so an "01:56" line precedes an "01:05" line.

## Commands

```powershell
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug -m:1
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release -m:1
.\tests\Run-Regression.ps1 -Configuration Debug -Name pcweb-command,panel-cladding-pc-commands
.\tests\Run-Regression.ps1                                   # every smoke, Debug and Release
.\tests\Package\Verify-PluginAssemblyIdentity.ps1 -Configuration Debug
.\tests\Package\Verify-PluginAssemblyIdentity.ps1 -Configuration Release
```

Live, in a second Rhino 8 started from the Terminal panel on a copy of the XYZ model named
`PCW - Wireframe LIVE TEST.3dm` (project code PCW, so the real `xyz` project in Wireframe Web is
not relinked), through MCP_Rhino:

```text
preview_rhino_script  libraryPath = <repo>\tests\Live\PcLive.cs, libraryType = PcLive,
                      script = "Result = PcLive.Start(doc, Parameters, Log);",
                      parametersJson = {"suite":"web","evidenceDir":"<repo>\\Project_Test\\261101_TEST_pcweb-command\\results",
                                        "assembly":"<repo>\\src\\PanelCladdingEditor\\bin\\Release\\net8.0-windows\\PanelCladdingEditor.rhp"}
apply_rhino_script    the returned token
```

The installed plug-in is 1.3.0 (no PCweb), so the suite loads the Release RHP built from this branch
into its own load context and calls `PanelCladdingWebCommand.Open(doc, askToSave)` with the
unsaved-changes answer given directly; the dialog and the `-PCweb` options are not driven.

## Results

| Run | Result |
| --- | --- |
| Production RHP build, Debug and Release | 0 warnings, 0 errors |
| `Verify-PluginAssemblyIdentity.ps1` Debug and Release | `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, one distinct id |
| `pcweb-command`, `panel-cladding-pc-commands` (Debug) | 2/2 passed |
| `Run-Regression.ps1` Debug, first build | 82/82 passed |
| `Run-Regression.ps1` Release, first build | 82/82 passed |
| `Run-Regression.ps1` Debug, final build | 82/82 passed ([`regression-Debug.json`](regression-Debug.json)) |
| `Run-Regression.ps1` Release, final build | 82/82 passed ([`regression-Release.json`](regression-Release.json)) |
| Live `web` suite, final build | 13/13 passed ([`results/web.json`](results/web.json)) |
| Live read-only check, final build except the shell start | passed ([`results/web-readonly.json`](results/web-readonly.json)) |

### Live `web` suite (final build, Rhino 56932)

| Check | Result |
| --- | --- |
| W.0 Wireframe Web 1.0.0 found through the registry `Launcher`; model saved, writable, unmodified | pass |
| W.1 saved model: no question, `PCweb: opening PCW in Wireframe Web...`, then `PCweb: PCW is open in Wireframe Web: http://127.0.0.1:8795/?source=api&project=pcw&user=nuofa` after 2 s; the project's `loadedSavedAt` equals the file's write time | pass |
| W.2 unsaved changes, answer No (LastSave): asked, nothing saved, the last save opened; a second run while the first waits prints `still opening` and starts nothing | pass |
| W.3 unsaved changes, answer Yes (Save): asked, saved (write time advanced, document unmodified), then opened; Wireframe Web loaded the new save | pass |
| W.4 unsaved changes, Cancel: asked, nothing saved, nothing started | pass |

The document state before and after every call is in the notes of `web.json`; it stays
`readOnly=False locked=True` throughout, and W.3 saves after two PCweb runs have started browsers.

## Finding: a launcher started with redirected output stops Rhino from saving

The first two live runs ([`results/web-run1-blocked.json`](results/web-run1-blocked.json) has the
first one's checks) stopped at W.3: `RhinoDoc.Save()` showed Rhino's modal "File Is Read-only"
message ("File opened in read-only mode. You must use SaveAs to save changes.") and the document
reported `IsReadOnly=True`, `IsLocked=False`, although W.0 had found it writable and locked. The
test drives no UI, so each blocked Rhino was stopped.

[`results/save-probe-inherited-handles.txt`](results/save-probe-inherited-handles.txt) narrows it
down with `tests/Live/SaveProbe.cs` in a fresh Rhino: the model saves after a fresh open, after
another process reads the whole file the way Node does, and after Wireframe Web loads it from a
launcher run outside Rhino. It does not save while a child process that Rhino started with
`UseShellExecute=false` and redirected output is running: that child inherits Rhino's inheritable
handles, including its lock on the open model. The Wireframe Web contract starts the launcher this
way, and the launcher starts Edge, which can outlive it; while it runs the model cannot be saved.

[`results/save-probe-shell-start.txt`](results/save-probe-shell-start.txt): a child started by the
shell (`UseShellExecute=true`, hidden window) inherits nothing, and the model saves while it runs.
`WireframeWebLauncher.Open` therefore starts `cmd.exe` through the shell and has it write the
launcher's output to files under `%TEMP%\PanelCladdingEditor\PCweb` (files older than a day are
removed on the next run). The final live run above uses this build.

## Read-only models

The first blocked Rhino also showed how PCweb treated a model Rhino cannot save: it asked, and Yes
ended in Rhino's read-only message. PCweb now skips the question when the modified model is
read-only and opens the last save. [`results/web-readonly.json`](results/web-readonly.json): on that
read-only, modified model the build with this change (before the shell start) printed
`PCW - Wireframe LIVE TEST.3dm is read-only; Wireframe Web opens its last save.`, asked nothing,
saved nothing and opened the project. `IsReadOnly` cannot be set from RhinoCommon, so this check is
not in the suite; the smoke checks the order in the source.

## Side effects

- Wireframe Web on this PC now has a project `PCW` linked to
  `C:\01_Projects\Panel Cladding Full\LiveTest-pcweb\PCW - Wireframe LIVE TEST.3dm`
  (Wireframe Web has no delete endpoint). The `xyz` and `abc` projects were not touched.
- Every successful run opened an Edge tab on the PCW project.
