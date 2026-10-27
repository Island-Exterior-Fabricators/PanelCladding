# Naming-convention sync from the main model only PLAN

## Background and authorization

On 2026-10-26 the user asked that only the project's main wireframe model,
`<PROJECT> - Wireframe.3dm`, trigger the naming-convention spreadsheet sync. Context:

- Since 1.1.0 `LivePanelNamingConventionSync` runs after every save of any `.3dm` whose file name
  yields a project code. A project can have several models that share one sheet, for example
  `XYZ - Wireframe.3dm` and `XYZ - Wireframe_STN.3dm` both write `XYZ - Naming Convention.xlsx`.
- The 1.2.0 EXET recorded the consequence: the struck (used/unused) state follows whichever model
  was saved last, and a secondary model adds its own wall types and frame configurations.

The request authorizes the PLAN -> EXET -> TEST records and the build. Packaging and installation
are not part of it.

## Goals

1. Only a model named `<code> - Wireframe.3dm` (any case, `<code>` = the file's project code)
   writes the PNL sheet on save.
2. Other models (`XYZ - Wireframe_STN.3dm`, `ABC - Wireframe - v2.3dm`, `ABC_old - Wireframe.3dm`,
   `abc-wireframe.3dm`) never write it. They still read it, so their frame-config codes keep
   following the recorded format and avoid codes the sheet already lists.
3. A secondary model with panels tells the user once per session why the sheet is not updated.

## Architecture ownership

- Application: `PanelCladdingProjectService.IsMainModelPath` and `MainModelFileName` (pure, testable
  without Rhino), beside `CodeFromDocumentPath`.
- Infrastructure/Rhino/Live: `LivePanelNamingConventionSync` gates the write.

## Design

- `IsMainModelPath(path)`: the extension is `.3dm` and the file name without extension equals
  `<code> - Wireframe` ignoring case, where `<code>` is `CodeFromDocumentPath(path)`. Requiring the
  whole name keeps suffixed copies (`_STN`, ` - v2`, ` (1)`) and names whose head is not purely the
  code (`ABC_old - Wireframe`) out.
- The save hook keeps its read part for every model: the sheet is read, its format remembered, the
  model's panel codes checked, and the sheet's Frame-Config rows reserved in the registry. Just before
  the Job Code check and the table build, a model that is not the main model returns; the first time
  per document session it prints
  `<sheet> is updated only when <CODE> - Wireframe.3dm is saved; <file> only reads it.`
- `PCtemplatePNL` is an explicit command and is not changed: it still configures the sheet from any
  saved model of the project.

## Affected files

`Application/Services/PanelCladding/PanelCladdingProjectService.cs`,
`Infrastructure/Rhino/Live/PanelCladding/LivePanelNamingConventionSync.cs`, the naming-convention
smoke (`Project_Test/261023_TEST_panel-cladding-naming-convention/Program.cs` and README), the package
README, a new `Project_Test/261026_TEST_naming-sync-main-model/README.md`.

## Acceptance

- Managed smoke: main-model names accepted (full project path, any case, numeric codes); secondary
  models, copies, versions, other extensions, autosave names, blank and code-less names rejected.
- Debug and Release builds; the naming-convention and layer-standard smokes pass in both.
- Live (inside Rhino, after the user installs a package): saving `XYZ - Wireframe_STN.3dm` leaves the
  sheet byte-for-byte unchanged and prints the note once; saving `XYZ - Wireframe.3dm` updates it.

## Rollback

Revert the two source files; the sheet itself is not touched by this change.

## Future extensions

A document string that marks another file name as the project's main model, if a project ever needs
a main model that does not follow the naming standard.
