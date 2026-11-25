PanelCladdingEditor 1.4.2 for Rhino 8 on Windows x64. It changes how `PCbake` treats existing
curves and panels that cannot be baked; every other command behaves as in 1.4.1. Read **Before
upgrading from 1.2.5** if you still run 1.2.5.

### Install without administrator access

1. Download **PanelCladdingEditor-1.4.2-win-x64.zip** (not the source-code ZIP).
2. Extract all files and close every Rhino window.
3. Double-click **Install.cmd** normally; do not use Run as administrator.
4. After installation and validation pass, start Rhino and run **PCeditor**.

To upgrade an earlier zip installation, follow the same steps. The installer replaces the previous
version and keeps it in a rollback folder. Do not also install through Rhino Package Manager.

Requires **Windows x64 and Rhino 8 running .NET 8**. Files install to
`%LOCALAPPDATA%\PanelCladdingEditor\plugin\1.4.2`, with registration only in `HKEY_CURRENT_USER`.
Close Rhino and run **Uninstall.cmd** to uninstall. **QUICKSTART.txt** has repair, validation and
SHA-256 verification commands.

### Changes in 1.4.2

- **PCbake overwrites existing curves.** With `Scope=Curves` or `All`, curves are reconciled like
  surfaces from the panel's current values: a curve that is still expected is overwritten in place
  (same object), missing curves are created and obsolete or duplicate ones deleted. A panel that
  already has curves is no longer skipped, and you no longer delete curves before baking. Hand
  edits made directly on baked curves are replaced, so run `PCsyncCrv` first to keep them.
- **PCbake skips a failing panel instead of stopping.** A panel with its own problem (no PID,
  locked or hidden, duplicate CID, a layout that cannot be generated, a layer name Rhino cannot
  create) is skipped and left unchanged. It is marked with a red dot on layer `PCbake Issues` that
  names the problem, listed on the command line and left selected; the other panels bake normally.
  Fix the panel and bake it again: its dot is removed. Problems that are not about one panel (an
  unsaved model, an unreadable material workbook) still stop the run. One Undo restores a run,
  dots included.

### Before upgrading from 1.2.5

- **Command names (1.3.0):** every command is `PC` plus camel case: `PCeditor`, `PCcreate`,
  `PCclear`, `PCcrvTemplate`, `PCmatchSrf`, `PCmatchCrv`, `PCbake`, `PCsyncSrf`, `PCsyncCrv`,
  `PCpid`, `PCreport`, `PCtemplatePNL`, and `PCweb` (1.4.0), 13 commands. Old spellings such as
  `PCEditor` still run.
- **PCbake (1.3.0)** replaces `PCupdate`, `PCspawnSrf` and `PCspawnCrv`, which no longer exist.
  Change aliases, toolbar buttons and macros that call them. `PCbake` has a `Scope` option
  (`Surfaces`, `Curves`, `All`) that starts at `Surfaces` on every run.
- **Not in Scope / By Others (1.3.0):** layers whose name contains these words (any case; spaces,
  `-` and `_` ignored) under `Surfaces - PNL` or `Curves - PNL` are not tracked: PCpid does not
  number their panels, PCbake creates no curves for them (in 1.4.2 such a panel is skipped and
  marked), the naming sheet and PCreport leave them out. Their auto values are kept current (1.4.0).
- **PCeditor (1.3.0):** the Extrusion view edits the layout only (merge, explode, hide, add,
  delete, dimensions). Profile assignment is suspended; existing `UD_FRAME_ASSIGNMENTS` are kept
  when extrusions are saved.

### Changes from 1.2.5 to 1.4.1

- New command **PCreport** (1.3.0): checks the active saved model's layers, panel attributes and
  managed surfaces and curves, read-only. Selecting rows selects and zooms to their objects;
  Rescan reads the model again; Export writes `<model> - PC Report.xlsx` (Summary and Issues).
- New command **PCweb** (1.4.0): opens the active saved model in Wireframe Web, the team's
  read-only web viewer, when it is installed (Wireframe Web 1.0.0 or later, a separate per-user
  install). The model must be named `<CODE> - ....3dm`. With unsaved changes PCweb asks: Yes saves
  first, No opens the last save, Cancel stops.
- 1.4.0 corrections: auto values are refreshed on Not in Scope / By Others panels; PCreport checks
  only objects the plug-in manages (26 rules); PCreport's Detail column fills the window.
- 1.4.1: the package README uses fictional project codes; the installer's registration names
  "Panel Cladding Editor contributors" as the organization and moves only the
  `PanelCladdingEditor` folder out of Rhino Package Manager's packages folder.

The full details are in the package README.

### Validation

Debug and Release builds passed with zero warnings and errors. The release check and the full
regression suite (83 smokes) passed in both configurations, including the PCbake checks for curve
overwrite and for skipped, marked panels. Compiled assembly/class GUID and version checks passed,
as did the payload hashes and the extracted-package install/repair/validate/uninstall test under
non-elevated Windows PowerShell 5.1.
