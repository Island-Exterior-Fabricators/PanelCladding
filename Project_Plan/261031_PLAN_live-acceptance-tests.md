# Live acceptance tests PLAN

## Background and authorization

On 2026-10-31, after installing 1.3.0, the user asked the agent to run the 1.3.0 manual test list
(`PanelCladdingEditor-1.3.0 Manual Tests.md`) itself, including the items that looked manual-only,
and to keep the scripts in the repository. The user must keep working on the same computer, so the
tests may not use keyboard or mouse input. The user approved programmatic UI Automation (patterns
only, no input) and any change to the test model. This authorizes the
test code and its PLAN/EXET/TEST records; it changes no plug-in behaviour.

## Goals

- Rerunnable live checks for each item of the 1.3.0 list, run against an installed plug-in on the
  test model, with evidence (command history, model state, screenshots, workbooks).
- No synthetic keyboard or mouse input; nothing outside the test model and Rhino is touched.

## Design

- Test model: the user's test model
  `C:\01_Projects\MCP_Rhino\.claude\worktrees\release-1.3.9\Project_Test\261031_TEST_release-1.3.9\XYZ - Wireframe LIVE TEST.3dm`,
  open in Rhino (the user allows any change to it; reopen this file if Rhino has to restart). A ABC
  copy for the speed check is at `C:\01_Projects\Panel Cladding Full\LiveTest-1.3.0\ABC\ABC - Wireframe.3dm`.
  The file name is not a main-model name, so the naming-convention sync does not run on save.
- In-process checks run as C# through MCP_Rhino's live scripting on the test document:
  `RhinoApp.RunScript` command macros (`_-PCbake _Scope=_All _Enter`, ...), command-history reads,
  model state (objects, user text, colours, selection, Undo, modified flag, viewport camera), the
  plug-in's WPF windows found in-process (named elements, list selection that raises the same
  handlers as a click), off-screen window renders and viewport captures.
- Out-of-process checks use Windows UI Automation patterns from PowerShell (Invoke, SelectionItem,
  Value; no `SendKeys`, no cursor moves) for the Save dialog and the discard prompt, and Excel
  through COM with `Visible = false` for exported workbooks and the open-file case.
- Command autocomplete is checked through Rhino's command registry (its data source); the popup's
  drawing is Rhino's own UI and is not tested.
- Scripts live in `tests/Live/` (not run by `Run-Regression.ps1`, marked as needing an installed
  plug-in, Rhino and a desktop session). Evidence goes to `Project_Test/<date>_TEST_live-acceptance-tests/`.

## Affected files

New `tests/Live/` (README, C# live scripts, PowerShell UI Automation and Excel helpers); `tests/README.md`
(Live folder row); the PLAN/EXET/TEST records. No `src/` change.

## Acceptance

- Each item of the 1.3.0 manual list has a script or a recorded reason it cannot be automated.
- A full run on the XYZ test model (and the ABC copy for speed) records pass/fail per item with evidence.
- No keyboard or mouse input is generated.

## Rollback

Delete `tests/Live/` and the records; nothing else depends on them.

## Future extensions

Run the same scripts for each release; add checks when commands change.
