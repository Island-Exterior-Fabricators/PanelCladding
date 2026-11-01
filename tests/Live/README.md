# Live acceptance checks

Checks for the manual test list of a release, run against the **installed** plug-in in a running
Rhino 8 on a test model. `Run-Regression.ps1` does not run them: they need the installed RHP, Rhino
and an interactive desktop session. They send no keyboard or mouse input, so the computer stays usable.

| File | Contents |
| --- | --- |
| `PcLive.cs` | One C# class run inside Rhino. Each suite is a list of steps run one per idle tick, outside any script host's undo record, the way typed commands run. Commands go through `RhinoApp.RunScript`; panels are picked with a preselection or the transparent `'_SelID`. WPF windows are read and driven by reflection (named elements, list selection, `Click` routed events) and rendered off screen. |
| `Watch-Dialogs.ps1` | Answers the Win32 dialogs a suite opens (Save As, overwrite prompt, MessageBox): finds them with `EnumWindows`, reads them with UI Automation, presses the button with Invoke or the `WM_COMMAND` it would send, and logs title, text, offered file name and folder. |
| `Hold-Workbook.ps1` | Opens the exported workbook in Excel through COM (`Visible = false`), records its sheets and holds it open for the export-while-open check. |
| `New-NamingSheet.ps1` | Writes a minimal `<CODE> - Naming Convention.xlsx` for the naming-convention check. |
| `Restart-Rhino.ps1` | Closes Rhino normally (`WM_CLOSE`, "Don't Save") and/or starts it on a model, then waits for the MCP_Rhino route. |
| `Show-Results.py` | Prints a suite's results file, one line per check. |
| `SaveProbe.cs` | Saves the open model on the next idle tick and logs its read-only and lock state around the save (`SaveProbe.Queue(doc, tag)`, `SaveProbe.Read()`); used to find why Rhino stopped saving after PCweb. |

## Suites

| Suite | Manual list | Needs |
| --- | --- | --- |
| `commands` | 1. Command names | - |
| `bake` | 2. PCbake | - |
| `excluded` | 3. Not in Scope / By Others | Saves and reopens the model. |
| `report` | 4. PCreport window, read-only, rules, navigation, Rescan | - |
| `scan` | 4. The first two `report` steps only (counts, read-only, rule dump) | - |
| `export` | 4. Export | `Watch-Dialogs.ps1 -Answers Save,Save,Yes` and `Hold-Workbook.ps1` running |
| `editor` | 5. PCeditor | `Watch-Dialogs.ps1 -Answers No,Yes` running |
| `otherdocs` | 4. Speed on a large model, another model, unsaved document; 3. naming convention | Run on the large model; `mainModel` = a main-model-named copy with a naming sheet beside it. Opens other documents. |
| `web` | PCweb: saved model, unsaved changes with LastSave / Save / Cancel, a second run while one waits | Wireframe Web installed; a saved model named `<CODE> - ....3dm` whose code is not a real project (PCweb links that project to it). `assembly` = a PanelCladdingEditor build to test when it is not the installed one. Saves the model; opens three browser tabs. |

Suites change the test model (they write cladding layouts on a few panels of
`XYZ-IEF-Wireframe::Surfaces - PNL::PNL-EXT-3A.1` and `PNL-EXT-3A.4`, bake, lock, hide and move
them, suffix duplicate PIDs); use a copy of a project model. Each setup step removes what an earlier
run left behind. Run `otherdocs` in a second Rhino so the main session keeps its model.

## Running through MCP_Rhino

Start Rhino from a normal user shell (not a sandboxed one: a Rhino started there may read a
redirected registry and not load its plug-ins), open the test model, select it with
`rhino_router_select_document`, then for each suite:

```text
preview_rhino_script  libraryPath = <repo>\tests\Live\PcLive.cs, libraryType = PcLive,
                      script = "Result = PcLive.Start(doc, Parameters, Log);",
                      parametersJson = {"suite":"bake","evidenceDir":"<repo>\\Project_Test\\<date>_TEST_live-acceptance-tests\\results"}
apply_rhino_script    the returned token
```

The suite runs after the apply returns; its results are written to `<evidenceDir>\<suite>.json`
(and `PcLive.Read(Parameters)` returns them while it runs). Several suites can be joined with `+`.
`excluded` and `otherdocs` reopen documents, so select the document again afterwards.

```powershell
python .\tests\Live\Show-Results.py .\Project_Test\<date>_TEST_live-acceptance-tests\results\bake.json --commands
```

For `export` and `editor`, start the helpers first from a normal user shell (Rhino's process id is in
`rhino_router_list_documents`):

```powershell
.\tests\Live\Watch-Dialogs.ps1 -ProcessId <pid> -Answers Save,Save,Yes -Log <evidenceDir>\export-dialogs.json
.\tests\Live\Hold-Workbook.ps1 -EvidenceDir <evidenceDir>
```

Never call `RhinoDoc.Undo()` from a step: outside a command it left the document with an open
undo record, and every later script was refused. Steps run the `_Undo` command instead.
