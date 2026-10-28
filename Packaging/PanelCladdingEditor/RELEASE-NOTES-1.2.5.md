PanelCladdingEditor 1.2.5 for Rhino 8 on Windows x64. This is the first published release since
1.0.97. It brings the project layer standard, new ID and naming conventions, the naming-convention
workbook sync and automatic panel attributes. Several of these changes require existing documents
to be migrated: read **Before upgrading** first.

### Install without administrator access

1. Download **PanelCladdingEditor-1.2.5-win-x64.zip** below (not the source-code ZIP).
2. Extract all files and close every Rhino window.
3. Double-click **Install.cmd** normally; do not use Run as administrator.
4. After installation and validation pass, start Rhino and run **PCEditor**.

To upgrade an earlier zip installation, follow the same steps. The installer replaces the previous
version and keeps it in a rollback folder. Do not also install through Rhino Package Manager.

Requires **Windows x64 and Rhino 8 running .NET 8**; Rhino 8.20 or newer includes .NET 8 by
default. No .NET SDK or MCP server is needed. Files install to `%LOCALAPPDATA%\PanelCladdingEditor\plugin\1.2.5`, with
registration only in `HKEY_CURRENT_USER`. Close Rhino and run **Uninstall.cmd** to uninstall. The
bundled **QUICKSTART.txt** has repair, validation and SHA-256 verification commands.

### Before upgrading from 1.0.97

Documents made with 1.0.97 are not recognized as they are. Try 1.2.5 on a copy first.

- **Layers (1.0.98):** panels, curves and cladding surfaces must be on the project layer standard
  (`<PROJECT>-IEF-Wireframe::Surfaces - PNL::...`, `Curves - PNL::...`,
  `Reference::Surfaces - STEP::...`). The `01_CW Panels`, `02_CW Extrusions`, `04_STEP Surfaces`
  and `03_Material Surfaces (STEP)` trees are no longer recognized.
- **IDs (1.0.99):** `-` separates the tokens of one ID and `_` comes before a role or component,
  for example `PID-ABC-N2-05-05` and `CID-ABC-N2-05-05_C_FRM-0`. The old `PID_ABC_N2_05_05` forms
  are no longer produced or matched.
- **Unit types (1.1.0):** `C`, `F`, `L`, `Z`, with the role taken from Definition A.
  `flat`/`corner_parent`/`corner_child` values and the old JSON frame configuration must be
  migrated.
- **Project code (1.1.3, 1.2.1):** the project code comes from the file name
  (`ABC - Wireframe.3dm` is `ABC`). Only saving `<PROJECT> - Wireframe.3dm` updates the
  naming-convention workbook.
- **Attributes:** cladding logic and frame configuration are `UD_CLADDING_LOGIC` and
  `UD_FRAME_CONFIG` (1.2.2); older keys are read and renamed on the next write. Frame/extrusion
  assignments are `UD_FRAME_ASSIGNMENTS` and the panel frame is `UD_PLANE` (1.2.5); assignment
  payloads stored in `CW_1.09_FRAME_TYPE` and the `Plane` user string are no longer read.
  Scripts and schedules that read these values need the new keys.
- **Models change on open (1.2.3, 1.2.5):** unit dimensions, area, weight and `UD_PLANE` are
  refreshed automatically, outside Undo, so Rhino may ask to save an unchanged model.

### Changes since 1.0.97

- New command **PCtemplatePNL** (13 commands in total) sets the PNL code formats. Saving the main
  model updates the PNL sheet of `<PROJECT> - Naming Convention.xlsx` next to it, in place,
  without replacing written values.
- Unit dimension, width, height, area (`CW_2.07_UNIT_AREA`) and weight are kept current on every
  panel; the AutoATT script is no longer needed.
- `UD_CLADDING_LOGIC` holds each cell's material and is the only cladding source read;
  `CW_4.xx` values are written as output only.
- Hand-assigned fields such as `CW_1.09_FRAME_TYPE` are never overwritten or deleted; PCpid adds
  placeholders only for missing setup keys. Roles are written in lowercase, and object names drop
  the leading `CID-<project>-` of any project (`CID-XYZ-S-04-36` becomes `S-04-36`).
- The plug-in can also be installed from a company-internal Rhino Package Manager source (see
  the package README); it is not published to the public McNeel server.

The full details are in the package README.

### Validation

Debug and Release builds passed with zero warnings and errors. The six release suites passed in
each configuration, and the full regression suite (77 smokes) passes in both. Compiled
assembly/class GUID and version checks passed, as did all 32 payload hashes and the
extracted-package install/repair/validate/uninstall test under non-elevated Windows PowerShell
5.1. Live in-Rhino acceptance of most changes since 1.0.97 has not been completed.
