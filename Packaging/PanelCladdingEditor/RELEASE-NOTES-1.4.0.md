PanelCladdingEditor 1.4.0 for Rhino 8 on Windows x64. It follows 1.2.5 and contains 1.3.0, which
renamed the commands, replaced three commands with `PCbake` and added `PCreport`, the corrections
from the 1.3.0 live acceptance run, and the new command `PCweb`. Read **Before upgrading** first.

### Install without administrator access

1. Download **PanelCladdingEditor-1.4.0-win-x64.zip** (not the source-code ZIP).
2. Extract all files and close every Rhino window.
3. Double-click **Install.cmd** normally; do not use Run as administrator.
4. After installation and validation pass, start Rhino and run **PCeditor**.

To upgrade an earlier zip installation, follow the same steps. The installer replaces the previous
version and keeps it in a rollback folder. Do not also install through Rhino Package Manager.

Requires **Windows x64 and Rhino 8 running .NET 8**. Files install to
`%LOCALAPPDATA%\PanelCladdingEditor\plugin\1.4.0`, with registration only in `HKEY_CURRENT_USER`.
Close Rhino and run **Uninstall.cmd** to uninstall. **QUICKSTART.txt** has repair, validation and
SHA-256 verification commands.

### Before upgrading from 1.2.5

- **Command names (1.3.0):** every command is `PC` plus camel case: `PCeditor`, `PCcreate`,
  `PCclear`, `PCcrvTemplate`, `PCmatchSrf`, `PCmatchCrv`, `PCbake`, `PCsyncSrf`, `PCsyncCrv`,
  `PCpid`, `PCreport`, `PCtemplatePNL`, and `PCweb` (1.4.0), 13 commands. Old spellings such as
  `PCEditor` still run.
- **PCbake (1.3.0)** replaces `PCupdate`, `PCspawnSrf` and `PCspawnCrv`, which no longer exist.
  Change aliases, toolbar buttons and macros that call them. `PCbake` has a `Scope` option
  (`Surfaces`, `Curves`, `All`) that starts at `Surfaces` on every run. A panel that already has
  curves keeps all of them (hand edits and assigned attributes stay); delete its curves to bake new
  ones.
- **Not in Scope / By Others (1.3.0):** layers whose name contains these words (any case; spaces,
  `-` and `_` ignored) under `Surfaces - PNL` or `Curves - PNL` are not tracked: PCpid does not
  number their panels, PCbake creates no curves for them, the naming sheet and PCreport leave them
  out. Their auto values are kept current (1.4.0).
- **PCeditor (1.3.0):** the Extrusion view edits the layout only (merge, explode, hide, add,
  delete, dimensions). Profile assignment is suspended; existing `UD_FRAME_ASSIGNMENTS` are kept
  when extrusions are saved.

### Changes since 1.2.5

- New command **PCreport** (1.3.0): checks the active saved model's layers, panel attributes and
  managed surfaces and curves, read-only. Errors, warnings and info are grouped by rule; selecting
  rows selects and zooms to their objects; Rescan reads the model again; Export writes
  `<model> - PC Report.xlsx` (Summary and Issues).
- New command **PCweb** (1.4.0): opens the active saved model in Wireframe Web, the team's
  read-only web viewer, when it is installed (Wireframe Web 1.0.0 or later, a separate per-user
  install). The model must be named `<CODE> - ....3dm`. With unsaved changes PCweb asks: Yes saves
  first, No opens the last save, Cancel stops. Rhino stays usable while Wireframe Web starts; the
  page address follows on the command line.
- **1.4.0 corrections:**
  - Auto values (unit dimension, width, height, area, weight, `UD_PLANE`) are refreshed on Not in
    Scope / By Others panels again.
  - PCreport checks only objects the plug-in manages; wireframe copies such as `Surfaces - GLS` or
    `Surfaces - TER` are no longer listed, even with a PID (26 rules).
  - PCreport's Detail column fills the window instead of being cut off.

The full details are in the package README.

### Validation

Debug and Release builds passed with zero warnings and errors. The release suites and the full
regression suite (82 smokes) passed in both configurations. Compiled assembly/class GUID and
version checks passed, as did the payload hashes and the extracted-package
install/repair/validate/uninstall test under non-elevated Windows PowerShell 5.1. 1.3.0 was accepted
live in Rhino on the XYZ and ABC test models, and PCweb live against Wireframe Web 1.0.0 before
packaging; the remaining 1.4.0 changes are covered by managed smokes and are accepted live after
installation.
