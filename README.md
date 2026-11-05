# PanelCladding

**PanelCladdingEditor** is a Rhino 8 plug-in for designing curtain-wall panel cladding. It numbers
facade panels, lays out each panel's frame grid and cladding cells, and bakes the result into
managed cladding surfaces and extrusion curves that stay in sync with the panel's attributes.

Windows x64 and Rhino 8 (.NET 8) only. No MCP server, .NET SDK or administrator access is needed.

## Features

| Command | What it does |
| --- | --- |
| `PCpid` | Numbers the selected facade panels: writes PID, CID, elevation and level from a north and a first-floor reference panel, and adds the standard setup keys. Run it first. |
| `PCtemplatePNL` | Records the project's PNL code format and lays out the PNL sheet of the naming-convention workbook. |
| `PCcreate` | Builds each selected panel's frame grid (horizontal and vertical intermediates) from guide curves and writes its width, height and dimension. |
| `PCeditor` | Opens the editor window: grid dimensions and offsets, cladding cells and materials (merge, split, assign), and the extrusion layout (merge, explode, hide, add, delete). Includes Material and Extrusion setup. |
| `PCcrvTemplate` | Applies a full-run horizontal or vertical merge pattern to panels that have none yet. |
| `PCmatchSrf` | Copies the grid, cladding cells and materials from a configured source panel to target panels. |
| `PCmatchCrv` | Copies the frame configuration (segment, merge and hide masks) from a source panel to target panels with the same grid. |
| `PCclear` | Clears the cladding assignments of the selected panels. |
| `PCbake` | Bakes the panels' saved settings into managed geometry. `Scope`: `Surfaces` (default), `Curves` or `All`. Surfaces are rebuilt, created or removed to match; existing curves are never overwritten. |
| `PCsyncSrf` | Reads edited cladding surfaces and material layers back into the panel. |
| `PCsyncCrv` | Reads edited extrusion curves back into the panel's grid. |
| `PCreport` | Checks the model's layers, panel attributes and managed geometry without changing anything. Click an issue to zoom to it; export to Excel. |
| `PCweb` | Opens the saved model in [Wireframe Web](https://github.com/Island-Exterior-Fabricators/wireframe-web/releases), the team's read-only web viewer, if it is installed. |

The plug-in also works in the background:

- **Panel auto attributes** - width, height, dimension, area, weight and the panel frame
  (`UD_PLANE`) stay current on every panel as panels are added, edited or moved.
- **Naming-convention sync** - saving the project's main model, `<CODE> - Wireframe.3dm`, updates
  the code tables in `<CODE> - Naming Convention.xlsx` next to it.

## How to use

The full command behaviour, attribute keys and code formats are in the
[package README](Packaging/PanelCladdingEditor/README.md). This is the short version.

### Before you start

- Save the model as `<CODE> - Wireframe.3dm`. `<CODE>` is the project code: `ABC - Wireframe.3dm`
  is project `ABC`. Every command needs a saved model.
- Draw each panel as a vertical, planar, four-corner surface facing outward, and put it on a panel
  type layer:

  | Objects | Layer |
  | --- | --- |
  | Source panels (you draw) | `<CODE>-IEF-Wireframe::Surfaces - PNL::Surfaces - <type>` |
  | Extrusion curves (baked) | `<CODE>-IEF-Wireframe::Curves - PNL::Curves - <type>` |
  | Cladding surfaces (baked) | `Reference::Surfaces - STEP::Surfaces - <prefix>::<material>` |

  Baked layers are created for you. Layers named `Not in Scope` or `By Others` are skipped.
- Keep `<CODE> - Naming Convention.xlsx` next to the model, and close it in Excel while you work.

### 1. Set up the project

Run `PCtemplatePNL` once per project. Answer the four code-format prompts (Wall-Type,
Frame-Config, Frame-Type, Cladding); it lays out the workbook's PNL sheet.

### 2. Number the panels

Run `PCpid`. Enter the project code, select the panels, then pick a north-facing panel and a
first-floor panel. Each panel gets its PID, CID, elevation, level, name and blank setup keys.
Number panels before baking: a panel with baked surfaces or curves cannot be renumbered until
they are deleted.

### 3. Set each panel's grid

Run `PCcreate`, select the panels, then select the guide curves that cross them. Each guide
becomes a horizontal or vertical intermediate track in the panel's grid.

### 4. Configure cladding and frames

Run `PCeditor` and select one panel. The editor has two views:

- **Cladding view** - drag a material from the sidebar onto a cell, or select a cell and use
  **Parent cell** to make it share another cell's material. Use **Setup** to choose the project
  workbook and manage its material catalogue.
- **Extrusion view** - select frame or intermediate curves and use **Merge**, **Explode**,
  **Hide**, **Add H**, **Add V** or **Delete**; type offsets to move dividers.

Save with **Save Cladding**, **Save Extrusions** or **Save Both** (needed when the grid size
changed). To repeat a configuration on other panels, run `PCmatchSrf` (cladding) or `PCmatchCrv`
(frames): select the target panels first, then the source panel. `PCcrvTemplate` applies a quick
full-run merge to panels that have no frame configuration yet.

### 5. Bake the geometry

Run `PCbake`, select the panels and choose `Scope`: `Surfaces` (default), `Curves` or `All`.
Panels need a `CW_1.05_LOT` value before their surfaces can be baked. Rerun `PCbake` after any
change: surfaces are updated in place. Curves are only created for panels that have none, so
delete a panel's curves first to bake them again.

### 6. Edit geometry by hand (optional)

You can split, merge or re-layer baked cladding surfaces, or edit baked curves, directly in Rhino.
Then select the panels and run `PCsyncSrf` (surfaces) or `PCsyncCrv` (curves) to read the changes
back into the panel, so the next `PCeditor` or `PCbake` starts from them.

### 7. Check and share

Run `PCreport` to list layer and attribute problems; click a row to zoom to the object, **Rescan**
after fixing, **Export** for an Excel copy. Run `PCweb` to open the saved model in Wireframe Web;
install Wireframe Web first (per user) from its
[releases page](https://github.com/Island-Exterior-Fabricators/wireframe-web/releases).

Every command that changes the model is a single Rhino Undo step.

## Install

### Requirements

- Windows x64.
- Rhino 8 running .NET 8. Rhino 8.20 or newer uses .NET 8 by default; if yours runs .NET
  Framework, run `SetDotNetRuntime`, choose .NET Core and restart Rhino.
- macOS is not supported.

### From the release zip (recommended)

1. Download **PanelCladdingEditor-&lt;version&gt;-win-x64.zip** from the
   [latest release](https://github.com/Island-Exterior-Fabricators/PanelCladding/releases/latest)
   (not the source-code zip).
2. Extract all files to a folder you can write to.
3. Close every Rhino window.
4. Double-click **Install.cmd**. Do not use Run as administrator.
5. When installation and validation pass, start Rhino and run `PCeditor`.

The plug-in installs to `%LOCALAPPDATA%\PanelCladdingEditor\plugin\<version>` and registers only
under `HKEY_CURRENT_USER`. If your organization blocks unsigned PowerShell scripts, ask IT.

**Upgrade:** repeat the same steps with the new zip. It replaces the previous version and keeps it
in a rollback folder. Read the release notes before upgrading across command changes.

**Uninstall:** close Rhino and double-click **Uninstall.cmd** in the extracted folder.

**Repair, validate or check the download:** see **QUICKSTART.txt** in the zip.

### From Rhino Package Manager (alternative)

If your team publishes the plug-in to an internal Package Manager source:

1. Close Rhino. If the zip version is installed, run its **Uninstall.cmd** first.
2. In Rhino, open `Options > Package Manager` and add the team's source folder to `Sources`.
3. Run `_PackageManager`, search `PanelCladdingEditor`, install it and restart Rhino.

Never install both the zip and the Package Manager versions; Rhino reports `ID already in use`.

## Build from source

Requires the .NET 8 SDK and Rhino 8 at `C:\Program Files\Rhino 8` (otherwise update the
RhinoCommon `HintPath` in the project).

```powershell
dotnet build .\PanelCladding.sln -c Release -m:1
.\tests\Run-Regression.ps1
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1
```

Packages are written to `Packaging/PanelCladdingEditor/artifacts/` (not tracked). Contributors
should read [AGENTS.md](AGENTS.md) and the [architecture guide](Project_Guides/PanelCladding%20Architecture.md)
first.

## Repository

- `src/PanelCladdingEditor/` - plug-in source: application logic, Rhino adapters and WPF UI.
- `Packaging/PanelCladdingEditor/` - package and installer scripts, release notes.
- `tests/` - regression smokes and package checks ([test runner](tests/README.md)).
- `Project_Plan/`, `Project_Exet/`, `Project_Test/` - development records (see the
  [plan log](Project_Guides/PanelCladding%20Plan%20Log.md)); completed ones move to `Project_Archive/`.
