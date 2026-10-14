First downloadable release from the standalone PanelCladding repository. The
version continues the existing PanelCladdingEditor 1.0.96 internal build sequence.

### Install without administrator access

1. Download **PanelCladdingEditor-1.0.97-win-x64.zip** below (not the source-code ZIP).
2. Extract all files and close every Rhino window.
3. Double-click **Install.cmd** normally; do not use Run as administrator.
4. After installation and validation pass, start Rhino and run **PCEditor**.

Requires **Windows x64 and Rhino 8 running .NET 8**. Rhino 8.20 or newer includes
.NET 8 by default; see [McNeel's runtime guide](https://developer.rhino3d.com/guides/rhinocommon/moving-to-dotnet-core/).
No .NET SDK or MCP server is needed. macOS is not supported by the WPF UI.

Files install to `%LOCALAPPDATA%\PanelCladdingEditor\plugin\1.0.97`, with registration
only in `HKEY_CURRENT_USER`. The launcher requests no elevation and makes no saved
execution-policy changes. Organization policies may restrict unsigned scripts.
Do not also install a copy through Rhino Package Manager.

Close Rhino and run **Uninstall.cmd** to uninstall. The bundled **QUICKSTART.txt**
includes repair, validation and SHA-256 verification commands. Keep the extracted
package for those operations.

### Included

- Standalone panel cladding editor, workbook integration, material/extrusion
  catalogues, and the existing twelve PC commands.
- Complete private managed and Windows x64 native dependencies.
- Installation validation, ownership-aware uninstall and registration rollback.
- ZIP checksum sidecar and an internal file-hash manifest.

### Validation

Debug and Release builds passed with zero warnings/errors; six managed suites
passed in each configuration. Compiled assembly/class GUID and version checks,
all 32 payload hashes, and extracted-package install/repair/validate/uninstall
passed under non-elevated Windows PowerShell 5.1. Failure injection verified
rollback. Native Rhino UI acceptance was not rerun for this packaging release.
