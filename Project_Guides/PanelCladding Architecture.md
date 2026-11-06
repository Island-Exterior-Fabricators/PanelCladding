# PanelCladding architecture

The current internal model of `PanelCladdingEditor`: its layers, the data it keeps in Rhino
documents, and the invariants a change must preserve. User-facing command behaviour and the full
attribute reference for users are in the [package README](../Packaging/PanelCladdingEditor/README.md);
build, packaging, installation and release are in [Operations](PanelCladding%20Operations.md); the
reasons behind deliberate choices are in [Decisions](PanelCladding%20Decisions.md).

Paths are relative to `src/PanelCladdingEditor/`. `Svc/` stands for
`Application/Services/PanelCladding/` and `Live/` for `Infrastructure/Rhino/Live/PanelCladding/`.
Every failure message starts with a stable upper-case code (`PANEL_CLADDING_...`); smokes assert
these codes, so keep them when changing a message.

## Repository layout

| Folder | Contents |
| --- | --- |
| `src/PanelCladdingEditor/` | The plug-in (this guide). |
| `tests/` | All rerunnable test code: managed smokes, package checks, static checks, live suites and `Run-Regression.ps1` ([tests/README.md](../tests/README.md)). |
| `Packaging/PanelCladdingEditor/` | Package, install, repair and uninstall scripts, the package README and release notes. |
| `Design/PanelCladdingEditor/` | Design references for the editor UI. |
| `Project_Guides/` | This guide, [Workflow](PanelCladding%20Workflow.md), [Operations](PanelCladding%20Operations.md) and [Decisions](PanelCladding%20Decisions.md). |

## Product boundaries

- `src/PanelCladdingEditor` is a standalone .NET 8 (`net8.0-windows`, WPF) Rhino 8 plug-in. It
  does not host MCP and must not reference MCP_Rhino or ModelContextProtocol. (The live acceptance
  suite in `tests/Live` is driven through an external MCP_Rhino script host; that is a test tool,
  not a dependency.) Older history may mention MCP_Rhino as its original execution context.
- RhinoCommon comes from the local Rhino 8 installation (`PanelCladdingEditor.csproj`,
  `Private=false`, alias `rhinocommon`) and is never shipped in a package.
- Identity: the assembly-level GUID (`AssemblyInfo.cs`), the `[Guid]` on
  `PanelCladdingEditorPlugin.cs`, the package manifest `pluginId` and the HKCU registration GUID
  must agree (`7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35`). Rhino reads the assembly-level attribute.
  Product identity is separate from the repository name.
- Command classes carry fixed `[Guid]`s that survive renames (`UI/*Command.cs`); the 13 command
  names and the `PC` + camel-case rule are owned by the `panel-cladding-pc-commands` smoke.
- A normal build produces the `.rhp`; `PanelCladdingTestHost=true` produces a `.dll` for smokes.
- There are two mutually exclusive install channels (zip/registry and Yak); a machine uses one,
  never both. See Operations.

## Project structure and dependency direction

| Folder | Contents | May depend on |
| --- | --- | --- |
| `Domain/` | Plain models and enums: `PanelCladdingModels.cs` (layout, cells, topology, plans, snapshots), `PanelCladdingUpdateModels.cs` (bake), `PanelCladdingPidModels.cs`, `PanelCladdingReportModels.cs`, `PanelCladdingNamingConventionModels.cs` | nothing |
| `Contracts/` | `OperationResponse` / `OperationResponse<T>` (`Success`, `Message`, `Data`), the only result type | nothing |
| `Application/Interfaces/` | Ports implemented by Infrastructure: `ILivePanelCladdingRepository`, the `ILivePanelCladding*Service` per command, `IPanelCladdingWorkbookRepository`, `IPanelPreviewRenderer`, `IPanelViewportHighlight`, `IPanelReportNavigator` | Domain, Contracts |
| `Application/Services/` | Pure rules and planners: `PanelCladdingKeyService` (+ `.FrameConfiguration.cs`), `PanelCladdingSaveService`, `PanelAxonometricProjectionService`, dormant `PanelCladdingTypeSignatureService`; `Svc/` holds one planner per command plus shared rules (CID, logic, regions, logical cells, topology normalization, coverage, layer predicates, naming formats, compliance) | Domain, Contracts |
| `Infrastructure/Rhino/` | All RhinoCommon access: `LivePanelCladdingRepository`, `LivePanelPlane`, `LivePanelViewportHighlight`, `LivePanelReportNavigator`; `Live/` one adapter per command plus the background services | Application, Domain, Contracts |
| `Infrastructure/File/` | OpenXML workbooks (catalogue, naming convention, PNL template, report export), PDF schedule import, preview renderer | Application, Domain, Contracts |
| `Infrastructure/WireframeWeb/` | `WireframeWebLauncher` (no RhinoCommon) | Application |
| `UI/` | Rhino command adapters (`*Command.cs`), WPF windows/dialogs, canvas, prompts | everything above |
| `PanelCladdingEditorPlugin.cs` | Entry point only: `LoadTime = AtStartup`, registers the two background services | Infrastructure |

Rules that keep this direction:

- Only files that declare `extern alias rhinocommon` can use Rhino types; they live in
  `Infrastructure/Rhino/`, `UI/` and the plug-in class. Application, Domain and Contracts have no
  Rhino or WPF reference. Infrastructure never references `UI`.
- Planners take snapshots (user-text dictionaries, measured geometry descriptors) and return plans;
  they never see Rhino objects. Example: `PanelCladdingPidPanel` carries a normal and boundary
  points, not a Brep (`Domain/PanelCladdingPidModels.cs`).
- Composition is explicit: each `UI/*Command.cs` `RunCommand` constructs its services with `new`.
  There is no DI container and no global service locator.

## Panel state: where data lives

A panel is a Brep under the panel layer branch. All of its state is object user text on that Brep
(plus its layer and object name). Keys are matched case-insensitively everywhere; a write deletes
every case variant and writes the canonical spelling. Conflicting case variants of a frame key fail
with `PANEL_FRAME_ATTRIBUTE_CONFLICT` (`PanelCladdingKeyService.FrameConfiguration.cs`). Key numbers
were reused across versions, so always match exact key names (for example `CW_1.10_CAD_TYPE` is a
setup key while `CW_1.10_CLADDING_TYPE` is retired; `CW_2.05`–`CW_2.07` once held masks and now
hold anchor, penetration and area keys).

### Key classes

| Key | Class | Format and rules | Owner file |
| --- | --- | --- | --- |
| `CW_2.03_OFFSET_H<i>`, `CW_2.04_OFFSET_V<i>` | Authority: grid | Cut positions in the panel frame, H from the bottom, V from the left; indices contiguous from 0 (gaps and duplicates fail); ascending inside the extent; rounded to 5 decimals `AwayFromZero`, stored `0.#####`, invariant culture; rewritten canonically on save | `Application/Services/PanelCladdingKeyService.cs` |
| `UD_CLADDING_LOGIC` | Authority: cladding | JSON over every physical cell label: material code (owner cell), owner label (member cell) or `""`, e.g. `{"0A":"AL-01","0B":"0A","1A":""}`; relaxed escaping. Present and nonblank = "configured"; its label set must equal the grid (`PANEL_CLADDING_LOGIC_GRID_MISMATCH`) | `Svc/PanelCladdingLogicService.cs`, `PanelCladdingKeyService.ReadCladdingCellValues` |
| `UD_FRAME_CONFIG` | Authority: topology | Readable text `<h>H <v>V \| delete … \| merge … \| hide …` (see below) | `Svc/PanelFrameConfigFormat.cs` |
| `UD_FRAME_ASSIGNMENTS` | Authority: extrusion assignments | JSON `{"v":2,"f":{FRM-n:[codes]},"s":[{a,t,b,c}],"d":{definitions},"x":{modifiers}}`; v1 still read | `Svc/PanelFrameAssignmentService.cs` |
| `CW_1.01_PID` | Authority: identity | `PID-<CODE>-<elevation>-<level>-<bay>`, e.g. `PID-ABC-N2-05-05` | `Svc/PanelCladdingPidPlanningService.cs` |
| `CW_1.02_CID` | Derived identity | `CID-` + PID body + role suffix; rewritten on every normalized write when the unit type is explicit, otherwise a stored custom CID is kept | `Svc/PanelCladdingCidService.cs` |
| `CW_1.06_UNIT_TYPE`, `CW_5.00_DEFINITION_A` | Hand input | Unit type `C`/`F`/`L`/`Z`; role `parent`/`child`/`child <n>` (stored lowercase; C/F are always `parent`) | `Svc/PanelCladdingCidService.cs`, `Svc/PanelCladdingPidSetupService.cs` |
| `CW_1.05_LOT`, `CW_1.07_WALL_TYPE` | Hand input, inherited | Copied to dependencies; both required to bake surfaces (`PANEL_CLADDING_LOT_NUMBER_REQUIRED`, `..._WALL_TYPE_REQUIRED`) | `Svc/PanelCladdingSpawnPlanningService.cs` |
| `CW_1.09_FRAME_TYPE`, `CW_6.01_PSF` | Hand input | Frame type is read only by the naming sync; PSF feeds the weight | `Live/LivePanelNamingConventionSync.cs`, `Svc/PanelCladdingAutoAttributeService.cs` |
| `CW_1.03_ELEVATION`, `CW_1.04_LEVEL` | PCpid output | Elevation `N1`, `E2`…; level two digits relative to the first-floor row (`01`, `00`, `-01`) | `Domain/PanelCladdingPidModels.cs` |
| `CW_4.<cc>_CLADDING_<c><R>` | Output only | One key per logical-cell representative, e.g. `CW_4.01_CLADDING_1A`; blank stored as one space. Never read, except that logic written before 1.2.4 (no materials) takes each root's material from it once | `PanelCladdingKeyService.GetCellKey`, `PanelCladdingSaveService.cs` |
| `CW_1.08_FRAME_CONFIG` | Derived code | Code for the `UD_FRAME_CONFIG` text in the project's Frame-Config format (default `AA`) | `Live/LivePanelFrameConfigCodes.cs` |
| `CW_2.00_UNIT_DIMENSION`, `CW_2.01_UNIT_WIDTH`, `CW_2.02_UNIT_HEIGHT`, `CW_2.07_UNIT_AREA`, `CW_6.02_UNIT_WEIGHT` | Derived (auto) | `<w>x<h>` and `F5` model units; area `F3` + ` sqf`; weight = PSF leading number × area, `F3`, or `" "` without PSF | `Svc/PanelCladdingAutoAttributeService.cs` |
| `UD_PLANE` | Derived, read by all | `{ox, oy, oz};{xx,xy,xz};{yx,yy,yz}`, 6 decimals: Z out of the facade, Y world-up in plane, X = Y×Z, origin lower-left. Kept current by the auto attributes; every command reads the stored frame first so all share one frame | `Svc/PanelCladdingPlaneFormat.cs`, `Infrastructure/Rhino/LivePanelPlane.cs` |
| Object name | Derived | `ShortName(CID)`: the CID without `CID-<CODE>-`, e.g. `N2-05-05_C` | `PanelCladdingCidService.ShortName` |

`PCpid` also adds blank placeholders (`" "`) for the 30 setup keys in
`PanelCladdingPidSetupService.RequiredKeys` and never overwrites a key that exists in any case,
except the identity and unit-dimension keys it owns.

Legacy keys, still read for old panels: `CW_9.01_CLADDING_LOGIC` and `CW_9.02_FRAME_CONFIG` (read
when the `UD_` key is blank, renamed on the next normalized write by `PlanUserDefinedKeyMigration`);
`CW_2.12_DELETE_MASK`, `CW_2.10_MERGE_MASK`, `CW_2.11_HIDE_MASK` (Base64 binary masks, 10-byte
header: version 1, kind 1/2/3, H/V track, column and row counts), JSON `{"v":1,"delete","merge","hide"}`
or readable text in `CW_1.08`, and `CW_2.09_FRAME_ASSIGNMENTS`. A normalized write converts the
masks into `UD_FRAME_CONFIG`, which then wins; the next topology write deletes them.
Not read and deleted when touched: `CW_2.13_CLADDING_LOGIC`, the retired type keys
`CW_2.14_CLADDING_TYPE`, `CW_2.13_CLADDING_TYPE`, `CW_1.10_CLADDING_TYPE`, `CW_4.00_CLADDING_TYPE`,
`Signature`, `CW_4.00_CLADDING_SIGNATURE`, and `CW_1.5D_FRAME TYPOLOGY`. `Plane`, `Config` and
`AUTO_panel` are only reported by PCreport. Assignment payloads left in `CW_1.09_FRAME_TYPE` by
1.2.0–1.2.4 are not read (PCreport rule A09).

### Value conventions

- A blank value is stored as one space, `PanelCladdingKeyService.PersistedBlankCellValue`, because
  Rhino does not reliably keep empty user strings; readers trim it to empty.
- Frame configuration decoding order (`PanelCladdingKeyService.DecodeTopology`): readable text in
  `UD_FRAME_CONFIG` (or `CW_9.02`), then readable text in `CW_1.08`; a code in `CW_1.08` without a
  readable configuration fails (`PANEL_FRAME_CONFIG_BACKUP_MISSING`); then JSON; then the binary
  mask keys. A panel with none of them is the full lattice with nothing merged or hidden.
- Writers (save, PCcreate, PCmatchCrv, PCcrvTemplate, PCsync) write only `UD_FRAME_CONFIG`
  (`EncodeFrameConfigurationUserText`) and delete the other topology keys; the code in `CW_1.08` is
  derived at write time (below). `EncodeNonDefaultTopology` (binary masks) is kept only for the
  `panel-cladding-sparse-topology` smoke.
- Retired cladding type and signature generation is suspended: saves and matches delete those keys;
  `PanelCladdingTypeSignatureService` and the workbook `_CLADDING_INDEX` sheets are dormant.

### Document-level strings

| Key | Holds | Owner |
| --- | --- | --- |
| `PanelCladdingEditor.WorkbookPath` | Material/extrusion catalogue workbook (`Materials`, `Extrusions` sheets) | `LivePanelCladdingRepository.cs`, `Infrastructure/File/OpenXmlPanelCladdingWorkbookRepository.cs` |
| `PanelCladdingEditor.FrameConfigCodes` | JSON registry of frame-config code ↔ text | `Live/LivePanelFrameConfigCodes.cs` |
| `PanelCladdingEditor.PNLFormat` | Mirror of the project's PNL format | `Live/LivePanelNamingFormat.cs` |
| `PanelCladdingEditor.NamingConventionPath` | Optional override of the naming sheet path | `Live/LivePanelNamingConventionSync.cs` |

## Grid and topology model

- **Tracks.** H tracks are horizontal dividers (`CW_2.03`), V tracks vertical dividers (`CW_2.04`).
  With h H and v V tracks the panel has h+1 rows and v+1 columns.
- **Cells.** Label = column number + row letters (`0A`, `1C`); columns count from 0 left to right,
  rows A, B, …, Z, AA … bottom to top (`PanelCladdingKeyService.GetRowLabel`, all 26 letters).
- **Segments.** One track crossing one bay: `(axis, track, bay)` (`PanelCladdingSegmentCoordinate`).
  The four frame edges are not segments and cannot be deleted, merged or hidden.
- **Topology state** (`PanelCladdingTopologyState`): `MissingSegments` (deleted), `MergeRuns`
  (axis, track, start–end bay) and `HiddenSegments`. Invariants enforced on decode and encode in
  `PanelCladdingKeyService.cs`: a hidden segment cannot be missing; a merge cannot cross a missing
  segment; a merge run is wholly hidden or wholly visible.
- **Text format** (`Svc/PanelFrameConfigFormat.cs`): H segments are track letter + bay number
  (`A1`), V segments track number + bay letter (`0B`); merge runs give first and last bay with an
  en dash (`A0–1`, a hyphen is also read). Sections with nothing are omitted.
- **Logical cells.** Deleting a segment joins the two cells it separates; connected groups are the
  logical cells, possibly non-rectangular. The representative is the lowest row, then lowest column
  (`Svc/PanelCladdingLogicalCellService.cs`; the editor's `UI/PanelCladdingCellTopology.cs` mirrors
  it). Merges and hides do not join cells. `Collapse` keeps representatives only; `Expand` gives
  members the representative's label.
- **Regions and parent cells** (`Svc/PanelCladdingRegionService.cs`). A cell value is a material, a
  cell label (reference to a parent) or blank. References are followed to a material; cycles,
  unknown or blank targets, disconnected regions and owners without material fail. The canonical
  owner is the region's lowest row, then lowest column; materials are upper-cased.
- **Blank cells** are allowed in the logic (`""`) and stored as `" "` in `CW_4`.
- **Track collapse.** A track whose segments are all missing is removed (offset dropped, cells and
  parents renumbered, masks rebuilt) until stable (`Svc/PanelCladdingTopologyNormalizationService.cs`),
  in the editor and again in `PanelCladdingSaveService`.
- **Editor segment ids** are `H|V:<offset>:<bay>` (`UI/PanelCladdingExtrusion.cs`), keyed by offset
  so selections survive track edits; they are mapped back to track indices on save.

## Managed geometry

Managed objects are generated from panel attributes by PCbake and read back by PCsyncSrf/PCsyncCrv.

| | Cladding surfaces | Extrusion curves |
| --- | --- | --- |
| Geometry | One Brep per cladding region, split from the panel face | Frame `FRM-0` bottom, `FRM-1` top, `FRM-2` left, `FRM-3` right; one curve per present, unmerged, visible segment; one joined curve per merge run; simplified (`SimplifyExtrusionCurve`) |
| Managed root | At or below `Reference::Surfaces - STEP` (any project) | `<CODE>-IEF-Wireframe::Curves - PNL` subtree, excluded layers out |
| Layer | `Reference::Surfaces - STEP::Surfaces - <material prefix letters \| Other>::<MATERIAL>` | Panel type path with `Surfaces - ` → `Curves - ` |
| CID (`CW_1.02_CID`) | `<panel CID>_<owner cell>`, e.g. `CID-ABC-N2-05-05_C_0A` | `<panel CID>_<code>`, e.g. `…_FRM-0`, `…_INT-B0` |
| Other keys | `CW_1.01_PID`, `CW_1.05_LOT`, `CW_1.07_WALL_TYPE`, `Cladding` (= layer leaf), `Merge_Mark` | `CW_1.01_PID`, `CRV` (code), `CW_1.05_LOT` when the panel has one, `Extrusions` + `0D-…`/`1D-…` formula keys |
| Colour | `ColorFromLayer`; layer colour from the workbook `Materials` sheet, else a category palette | `ColorFromObject`: frame 0,0,255; H 128,0,128; V 0,100,0 |
| Name | `ShortName(CID)` | `ShortName(CID)` |
| Planner | `Svc/PanelCladdingSpawnPlanningService.cs` | `Svc/PanelCladdingExtrusionPlanningService.cs` |

- **Curve codes.** H intermediates `INT-<track letter><bay>`, V intermediates
  `INT-<track><bay letter>`; a merge over all bays `INT-<track>`, a partial one
  `INT-<track><start>–<end>`. Missing and hidden segments get no curve.
- **Identity separators** (`Svc/PanelCladdingCidService.cs`): `-` joins tokens of one identifier,
  `_` joins a CID to its role and component (`IdSeparator`, `ComponentSeparator`, `Component()`).
  Role suffixes `_P`, `_C`, `_C<n>` apply only to unit types L and Z; C and F have none; a missing
  or unknown unit type leaves the stored CID alone. A CID is never written without a PID.
- **Ownership.** A dependency belongs to a panel when its PID equals the panel PID and its role
  component matches the panel CID (`PanelCladdingCidService.IncludesDependency`); unsuffixed legacy
  dependencies match any role. PCsync additionally requires geometric association with exactly one
  selected panel (`LivePanelCladdingGeometryPartitionService.IsAssociated`; ambiguity is an issue).
- **CID scope.** CIDs are re-derived, never edited directly: changing PID, unit type or role and
  running any normalizing write updates the panel CID and name; PCbake rebuilds surfaces with new
  CIDs; PCsync reindexes dependency PID, CID, `CRV`, name, lot and colour without moving geometry.
  Selected panels sharing a CID are skipped by PCbake and left selected
  (`Svc/PanelCladdingUpdateSelectionService.cs`); duplicate selected PIDs fail PCsync.
- **Lot (release).** A dependency's `CW_1.05_LOT` mirrors the panel: case variants removed, absent
  when the panel has none, drift alone counts as a change.
- **Coverage metadata.** `Merge_Mark` = owner cell first, then the other covered cells by column and
  row, upper case, `;`-separated (`Svc/PanelCladdingSurfaceCoverageService.cs`).
  `CW_1.03_CLADDING_CELLS` is a read-only alias; differing values fail; sync rewrites `Merge_Mark`.
- **Bake reconciliation** (`Svc/PanelCladdingDependencyReconciliationService.cs`). Within the
  selected `Scope` (`Surfaces` default, `Curves`, `All`): surfaces matching by kind + PID + CID are
  replaced in place, missing ones created, stale or duplicate ones deleted. Curves are never rebuilt
  or deleted: a panel that owns any managed curve keeps all of them; only panels without curves get
  new ones. Objects outside the scope are untouched.
- **Locked and hidden objects** (`Live/LivePanelCladdingUpdateService.cs`). Dependencies are found
  including locked and hidden ones (reference objects excluded) and changed with
  `Replace(..., ignoreModes: true)` / `Delete(..., ignoreModes: true)`, keeping the object's `Mode`.
  Source panels must not be locked, hidden or reference objects.
- **Point order.** PCbake and PCpid rebuild straight quad panels to the standard corner order in
  the same Undo scope (`Svc/PanelCladdingPointOrderService.cs`, `Live/LivePanelCladdingPointOrderService.cs`);
  unsupported shapes are skipped with a warning.

## Layers and naming

- **Project code** (`Svc/PanelCladdingProjectService.cs`): from the saved file name, the text before
  the first ` - `, cut at the first non-alphanumeric character, upper case (`ABC - Wireframe.3dm` →
  `ABC`); empty for an unsaved document. PCpid prompts for a three-letter code, defaulting to it.
- **Layer standard** (`Svc/PanelCladdingExtrusionPlanningService.cs`): the root is the top-level
  `<CODE>-IEF-Wireframe` (any `*-IEF-Wireframe` when there is no code); panels live on
  `<root>::Surfaces - PNL::Surfaces - <type>…`; curves on `<root>::Curves - PNL::Curves - <type>…`.
  A panel directly on `Surfaces - PNL` cannot route curves (`PANEL_CLADDING_PANEL_TYPE_LAYER_REQUIRED`).
  Adapters call these predicates (`IsPanelLayerPath`, `IsCurveLayerPath`, `IsPanelBranchPath`,
  `ResolveCurveLayerPath`) instead of parsing paths themselves.
- **Excluded keywords.** A path any of whose segments contains `Not in Scope` or `By Others`
  (letters and digits compared, case ignored) is outside the panel and curve scopes
  (`ExcludedLayerKeywords`, `IsExcludedLayerPath`); curve routing fails with
  `PANEL_CLADDING_PANEL_LAYER_EXCLUDED`. Only the auto attributes still cover excluded panel layers
  (`IsPanelBranchPath`).
- **Retired trees** (`01_CW Panels`, `02_CW Extrusions`, `03_Material Surfaces (STEP)`,
  `04_STEP Surfaces`) are not recognized; only PCreport names them (`Svc/PanelCladdingComplianceService.cs`).
- **PNL format** (`Svc/PanelNamingFormat.cs`): per segment (Wall-Type, Frame-Config, Frame-Type,
  Cladding, Definition) one token of `AA`, `A`, `A1`, `01`, `001`; letters exclude I and O;
  Definition is always `01`. PCtemplatePNL records it in the workbook custom property
  `PanelCladdingEditor.PNLFormat` (`Infrastructure/File/OpenXmlPanelNamingTemplate.cs`); without
  one the legacy format applies.
- **Frame-config codes** (`Svc/PanelFrameConfigCodeRegistry.cs`, `Live/LivePanelFrameConfigCodes.cs`):
  per document, seeded from the sheet's Frame-Config rows, the stored registry and panels that
  already carry a code with its text. Codes are never reassigned; `AA`/`A1` codes group by H/V
  count (first letter). A code in another format on a model without a recorded format is left as it
  is and reported.
- **Naming-convention sync** (`Live/LivePanelNamingConventionSync.cs`): on `EndSaveDocument` of a
  real `.3dm` save (not export-selected or autosave). The sheet is
  `<CODE> - Naming Convention.xlsx` beside the model unless the document string overrides it. Every
  model reads it (format and reserved codes); only the main model `<CODE> - Wireframe.3dm` (whole
  name, any case; `PanelCladdingProjectService.IsMainModelPath`) writes it. It writes sheet `PNL`
  only: the Unit-Type, Wall-Type, Frame-Config and Frame-Type tables located by their row-12 titles
  (codes from row 13) and the Job Code cells under the row-7 header
  (`Infrastructure/File/OpenXmlPanelNamingConventionWorkbook.cs`). Codes keep their rows, written
  descriptions are never changed, only the struck state of unused rows changes, new codes go below.
  Usage comes from non-excluded, non-reference panels. An unchanged sheet is not rewritten; writes
  go to a temporary copy moved over the original (`Infrastructure/File/OpenXmlPnlSheet.cs`); a
  locked sheet is reported and retried on the next save. The model is never modified by the sync.

## Command pipeline

1. **Adapter** (`UI/*Command.cs`): requires a saved active document, selects objects, composes
   services and passes `doc.Path` and object ids.
2. **Read**: the `Live/` service resolves `RhinoDoc.ActiveDoc` and requires its path to equal the
   passed path (`FILE_NOT_ACTIVE`, `ACTIVE_DOC_UNSAVED`), then builds Domain snapshots.
   `LivePanelCladdingRepository.ReadLayout` measures the panel in its frame and parses keys.
3. **Plan**: a pure `Svc/` planner validates the whole batch and returns writes/deletes. Nothing is
   mutated when any panel fails (PCsync instead reports per-panel issues, skips those panels and
   selects them).
4. **Prepare**: proposed `ObjectAttributes` are duplicates of the originals with the plan applied,
   then `LivePanelCladdingCidService.Normalize`: rename `CW_9.xx` to `UD_` keys, lowercase the role,
   write canonical `UD_FRAME_CONFIG` and its `CW_1.08` code, rewrite the panel CID and name. The
   editor save, PCcreate, PCmatchSrf/Crv, PCsync and PCbake (source panels) normalize fully;
   PCcrvTemplate applies only the `UD_` rename and the frame-config code; PCpid writes identity
   itself; PCclear only deletes.
5. **Apply** in one Undo record named for the operation (`BeginUndoRecord`/`EndUndoRecord`), e.g.
   "Save Panel Cladding Attributes", "Clear Panel Cladding", "Match Panel Cladding". PCbake and
   PCpid reuse an open command record (`CurrentUndoRecordSerialNumber`) and roll back only what they
   changed; when PCbake owns the record it undoes it on failure.
6. **Verify and roll back**: on any failed write the service restores the original attributes in
   reverse order (`ROLLBACK_FAILED` when that fails). PCmatchSrf/Crv and PCsync read the written
   values back and restore the batch on mismatch. One redraw at the end.

Stale-edit guard: the editor keeps a fingerprint (SHA-256 of the Brep CRC, layer index and the
`CW_2.*`, `CW_4.*`, `UD_*`, `CW_9.*` and `CW_1.08` values; `LivePanelCladdingRepository.BuildFingerprint`)
and saves fail with `PANEL_CLADDING_STALE_EDITOR` when it changed. PCsync re-checks it per panel.

### Background services

Registered once in `PanelCladdingEditorPlugin.OnLoad`; they are not commands.

- **Auto attributes** (`Live/LivePanelAutoAttributes.cs`, values in
  `Svc/PanelCladdingAutoAttributeService.cs`). Object add, replace and attribute changes (layer or
  an input key: the auto keys or `CW_6.01_PSF`) only queue ids; a full scan runs after opening a
  document (not merge or reference) and after a layer rename or reparent. Writes happen on
  `RhinoApp.Idle` when no command runs, never during Undo, with quiet `ModifyAttributes`, and only
  for keys whose value differs (a `UD_PLANE` describing the same frame is kept). Scope: every
  non-reference panel in the panel branch, excluded layers included.
- **Naming-convention sync**, above.
- Per-document caches (PNL format, frame-config registry) are keyed by runtime serial number and
  dropped on `CloseDocument`; PCtemplatePNL clears them.

## Model-level commands

| Command | Reads | Writes | Planner / adapter |
| --- | --- | --- | --- |
| PCcreate | Panels + H/V guide curves | Offsets, `UD_FRAME_CONFIG` (no merges), unit dimensions, CID; deletes old offsets, topology, assignments, retired keys; deletes the logic only when it no longer fits the grid | `Svc/PanelCladdingCreatePlanningService.cs`, `Live/LivePanelCladdingCreateService.cs` |
| PCeditor save | Layout | Scope `Extrusions`: offsets, topology, assignments, dimensions; `Cladding`: logic + `CW_4`; `Both` | `Application/Services/PanelCladdingSaveService.cs` |
| PCclear | Panels | Deletes cell, logic, topology, assignment and retired keys; keeps offsets and identity | `Svc/PanelCladdingClearPlanningService.cs` (`PanelCladdingKeyService.IsClearableCladdingAssignmentKey`) |
| PCmatchSrf | One configured source, targets | Target `CW_4` + logic only (offsets and topology stay); the same physical grid copies the source graph verbatim, otherwise a region mapping from `Svc/PanelCladdingMatchFeasibilityService.cs`; an unconfigured source fails `PANEL_CLADDING_MATCH_SOURCE_NOT_CONFIGURED` | `Svc/PanelCladdingMatchPlanningService.cs`, `Live/LivePanelCladdingMatchService.cs` |
| PCmatchCrv | Source, targets with equal H/V counts | `UD_FRAME_CONFIG` + `UD_FRAME_ASSIGNMENTS`; offsets and cladding stay | `Svc/PanelCladdingCurveMatchPlanningService.cs` |
| PCcrvTemplate | Panels, H or V priority | Maximal merge runs on the priority axis, split at missing segments and hide changes; panels already merged are skipped (all skipped = success, no Undo) | `Svc/PanelCladdingCurveTemplatePlanningService.cs` |
| PCsyncSrf | Panels, associated surfaces (and curves) | Offsets from surface edges plus stored tracks protected by complete `Merge_Mark` coverage (unmarked stored tracks without geometric evidence are dropped); tracks aligned to the stored layout and masks remapped (`Svc/PanelCladdingLayoutReconciliationService.cs`); owner graph and logic from coverage and material layers (a saved owner is kept when the covered cells resolve to it); surface PID/CID/`Cladding`/`Merge_Mark`/lot/name | `Svc/PanelCladdingSurfaceSyncPlanningService.cs`, `Live/LivePanelCladdingSurfaceSyncRepository.cs` |
| PCsyncCrv | Panels, associated curves | Curve-inferred offsets (authoritative) and topology from curve spans; stored hidden segments without a curve stay hidden; curve PID/CID/`CRV`/lot/colour/name; cladding keys only on configured panels | same |
| PCbake | Panels | Managed surfaces/curves (above), panel CID, point order | `Live/LivePanelCladdingUpdateService.cs` |
| PCpid | All panels in the project panel scope as context | PID, CID, elevation, level, placeholders, name, point order; selected panels only | `Svc/PanelCladdingPidPlanningService.cs`, `Live/LivePanelCladdingPidService.cs` |
| PCreport | Whole model, read-only | Nothing; optional `.xlsx` export (`Summary`, `Issues`) | `Svc/PanelCladdingComplianceService.cs` (rules L01–L07, A01–A13, D01–D06) |
| PCtemplatePNL | Naming sheet | Sheet layout and `PNLFormat` property; document mirror | `Infrastructure/File/OpenXmlPanelNamingTemplate.cs` |

The compliance rules reuse the commands' own predicates and services, so the report and the commands
agree; add a rule there when you add a document invariant.

## UI architecture

- WPF, code-behind; there are no view models. `UI/PanelCladdingEditorController.cs` is the only
  bridge from the window to Application services (load, project, save, catalogue, PDF import).
- `UI/PanelCladdingEditorCommand.cs` keeps a single modeless `PanelCladdingEditorWindow`, owned by
  Rhino's main window (`WindowInteropHelper.Owner = RhinoApp.MainWindowHandle()`); rerunning loads
  the new panel into it.
- The window holds the edit session: values, offsets, deleted/merged/hidden segment ids, its own
  snapshot undo stack, and dirty flags per scope. Edits are previews until Save; Save Cladding is
  refused while structural edits are unsaved; a grid-size change on a configured panel needs Save
  Both (`PANEL_CLADDING_LOGIC_REQUIRES_SAVE_BOTH`, `PanelCladdingSaveService.cs`).
- `UI/PanelCladdingGridCanvas.cs` (`Canvas`, custom `OnRender`) draws cells, segments and
  dimensions and raises events (drop, dimension edit, placement, zoom); it never writes. It resolves
  owner boundaries with the same `PanelCladdingLogicalCellService` and `PanelCladdingRegionService`
  as save: edges between logical cells of one region are dashed.
- Extrusion assignment is suspended by `_extrusionAssignmentEnabled = false` in
  `PanelCladdingEditorWindow.xaml.cs`; handlers, payloads and save are kept.
- Viewport feedback goes through `IPanelViewportHighlight` (`LivePanelViewportHighlight`, a
  read-only display conduit retargeted after each successful load). PCreport's modeless window
  navigates through `IPanelReportNavigator`.
- Dialogs (`MaterialSetupDialog`, `ExtrusionSetupDialog`, `MaterialCategoryDialog`,
  `PanelNumericEditorDialog`) take injectable prompts: `IMaterialColorPicker` (Rhino colour dialog,
  hence the WindowsForms framework reference), `IMaterialCategoryPrompt`,
  `IExtrusionCatalogueClearPrompt`.

## Test seams

- Smokes (`tests/Smoke/<capability>/`) reference the project with `PanelCladdingTestHost=true` and
  copy RhinoCommon locally; all build into `artifacts/tests/bin/<Configuration>/`, so build them
  serially and rebuild the production project before packaging.
- Pure planners are tested directly with in-memory user-text dictionaries; shared fixtures are in
  `tests/Shared/` (`CladdingLogicFixture.cs`, `RegressionPaths.cs`).
- Ports replaced by fakes: `ILivePanelCladdingRepository` (capturing/stub repositories for the
  editor and save), `ILivePanelCladdingSurfaceSyncRepository`, `IPanelCladdingWorkbookRepository`,
  `IPanelReportNavigator`, the three prompt interfaces; `IPanelViewportHighlight` is optional.
- WPF windows and the canvas render off-screen in-process; private switches and fields are reached
  by reflection. There is no `InternalsVisibleTo`, so `internal` Live services are covered by the
  live suite (`tests/Live`, inside Rhino, not part of `Run-Regression.ps1`).
- Parts that need a Rhino host print `[SKIP]` in a plain run; a managed pass is not live acceptance.

## Changing the model safely

- New panel key: add the constant in `PanelCladdingKeyService.cs` (or the owning service), decide
  its class (authority, derived, output, hand input), include it in the fingerprint if the editor
  depends on it, in `IsClearableCladdingAssignmentKey` if PCclear should remove it, and in a
  compliance rule if it can be wrong in a model.
- Renaming a key: keep reading the old name, migrate on the next normalized write, never reuse a
  number that old panels may still carry with another meaning.
- Writes that change cladding, topology or identity go through `LivePanelCladdingCidService.Normalize`;
  every mutating command plans the whole batch first and applies it in one named Undo record
  (see Command pipeline).
- Update the package README, this guide and the smokes the change affects (see the
  [workflow](PanelCladding%20Workflow.md)).
