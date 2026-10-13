# PanelCladding architecture

`src/PanelCladdingEditor` is a standalone .NET 8 Windows/WPF Rhino 8 plug-in.
It does not host MCP and must not reference MCP_Rhino or ModelContextProtocol.

- `Domain/` contains product models and values.
- `Application/` contains services, planning and interface abstractions.
- `Infrastructure/` owns RhinoCommon access, workbook/PDF/file integration.
- `Contracts/` contains exchanged requests/results.
- `UI/` contains WPF surfaces and Rhino command adapters.
- `Packaging/PanelCladdingEditor/` owns packaging, install, repair and uninstall.
- `Design/PanelCladdingEditor/` contains design references.
- `Project_Test/` contains dedicated regression projects and historical evidence.

Preserve existing boundaries and transaction/Undo semantics. Managed tests opt into
`PanelCladdingTestHost=true` to produce a DLL; Rhino builds produce the RHP directly.
Serialize builds that use different values of that property, then rebuild the
production project before packaging. RhinoCommon is supplied by the local Rhino 8
installation and must not be shipped in the package.

The assembly-level GUID, plug-in class GUID, package manifest pluginId and HKCU
registration GUID must agree. Product identity is separate from the repository name.
Install under `%LOCALAPPDATA%\PanelCladdingEditor\plugin\<version>`, registry-only
with DirectoryInstall=0 and startup loading; never also install in Package Manager
discovery. Follow the AGENTS.md independent host attestation gates for activation.

Historical reports may refer to MCP_Rhino and original GitHub commits. They describe
the original execution context and are not current build dependencies.
