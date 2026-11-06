# PanelCladding decision log

This log records why PanelCladdingEditor works the way it does where the code alone does not say. Many of these choices were made after a live failure or at the user's explicit request, and several look odd or redundant until you know the reason. Read the entry before you reverse one of them. If you still change it, add a new entry and mark the old one superseded rather than rewriting it. Entries are in chronological order by the date each decision was first taken, and later refinements of the same decision are folded into its entry. Key, command and file names are today's names unless an entry says otherwise. The original development records are kept at git tag `records-2026-11-06`.

### 2026-08-04 — Standalone plug-in with a fixed identity

**Decision:** PanelCladdingEditor is a standalone Rhino 8 plug-in. It has no MCP_Rhino, Router, Transport, MCP SDK or dependency-injection dependency, and since 2026-10-13 it has its own repository. The product name, the plug-in GUID `7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35` and the command GUIDs do not change.

**Why:** The first version was built inside `MCP_Rhino.Server`, and the user then required the editor to be independent of MCP_Rhino. The current GUID replaced a development identity that Rhino had stored and kept reporting as "ID already in use". A new GUID makes Rhino treat the plug-in as a different product.

**Status:** current

### 2026-08-04 — H/V offsets are orthogonal distances in the panel frame

**Decision:** `CW_2.03_OFFSET_H<n>` and `CW_2.04_OFFSET_V<n>` are distances from the lower-left corner of the panel envelope, measured in the panel's local frame. This holds on curved panels too: they are never UV or arc-length distances. If the grid cannot be projected cleanly onto the geometry, the panel is refused rather than shown with a fake flat grid.

**Why:** That is the meaning the original Grasshopper workflow and its users gave these keys, and a key's meaning must not change with curvature. A misleading grid is worse than a refusal.

**Status:** current

### 2026-08-04 — One Undo record per command, all-or-nothing batches

**Decision:** Each command that changes the document commits in one named Undo record and opens none when nothing changes. It validates every selected object before changing any, and if a later write fails it restores the objects it already changed. Since 2026-09-03, services that can run inside a command (bake, PCpid) join the record Rhino already has open (`CurrentUndoRecordSerialNumber`). They end or undo only a record they opened themselves.

**Why:** The user should recover with one Undo. Rhino has no transaction across many objects, so a half-applied batch is hard to diagnose. Inside a command Rhino refuses a nested `BeginUndoRecord`, and closing the command's own record breaks Rhino's tracking of who owns it.

**Status:** current

### 2026-08-05 — Plug-in id comes from the assembly-level GUID; the RHP is built directly

**Decision:** `AssemblyInfo.cs` declares `[assembly: Guid(...)]`, equal to the plug-in class `[Guid]`, the package manifest `pluginId` and the HKCU key. The project compiles straight to `.rhp` (`TargetExt`). A package that also contains a same-name `PanelCladdingEditor.dll` is rejected.

**Why:** RhinoCommon reads only the assembly-level attribute. Without it each such RHP gets `Guid.Empty` and the second one fails to load, and an early test passed because it checked the class GUID instead. A DLL copied to `.rhp` kept a `.deps.json` that named the DLL, so no command registered. An RHP beside a same-name DLL is found twice.

**Status:** current

### 2026-08-05 — Registry-only per-user install, loaded at startup

**Decision:** The zip installer places the plug-in under `%LOCALAPPDATA%\PanelCladdingEditor\plugin\<version>` and registers it under HKCU with `LoadMode=1` and `DirectoryInstall=0`. The plug-in loads `AtStartup`, and the installer moves any Package Manager copy aside. While Rhino runs, the installer only stages the bundle and never replaces loaded files.

**Why:** A second copy under Package Manager caused "ID already in use" and missing commands, so Rhino must have exactly one place to find the plug-in. Loading at startup registers the commands before first use, and Rhino cannot replace a plug-in that is already loaded.

**Status:** current

### 2026-08-12 — Merged regions are owner and reference cells

**Decision:** A cell whose value is another cell's label (for example `1A` holding `0A`) belongs to that owner's region. The owner is the cell in the lowest row, then the lowest column. Regions must share edges, and a reference to a blank or unknown cell is an error. Deleting an intermediate segment merges the cells on both sides, and the same rule picks their representative.

**Why:** Neighbouring cells can form one physical piece across a divider, and the panel's own user text must describe that completely. A fixed rule for the representative keeps keys stable and matches what the editor shows.

**Status:** current (stored in `UD_CLADDING_LOGIC` since 2026-10-27)

### 2026-08-12 — Native WPF editor owned by Rhino's main window

**Decision:** The editor is a single-instance WPF window (`net8.0-windows`, `UseWPF`) whose owner is `RhinoApp.MainWindowHandle()`. It uses no Eto, WebView, HTML or Rhino dock panel.

**Why:** The design handoff had to become a real desktop window rather than an embedded web page, and the plug-in targets only Rhino 8 on Windows. The first editor was an Eto window.

**Status:** current

### 2026-08-13 — The workbook is a project material catalogue, not panel data

**Decision:** Cell, offset and layout data live only in Rhino user text. The `.xlsx` holds only the `Materials` and `Extrusions` sheets the plug-in manages, and saving a panel never opens it. Workbook writes go through a temporary copy, touch only those sheets, and use OpenXML rather than Excel automation.

**Why:** Material setup must be shared across a project. The earlier per-type preview sheets made Excel part of every save and were not useful. Rhino Undo cannot reverse a write to an external file.

**Status:** current

### 2026-08-18 — Narrow WindowsForms framework reference

**Decision:** The project references `Microsoft.WindowsDesktop.App.WindowsForms` as a `FrameworkReference` and must not set `UseWindowsForms`.

**Why:** RhinoCommon's `ShowColorDialog` overloads need WinForms metadata (`IWin32Window`). `UseWindowsForms` adds global usings whose `Color`, `Brush`, `Point` and input types clash with WPF throughout the code.

**Status:** current

### 2026-08-18 — PCcreate reads the grid from guides conservatively

**Decision:** PCcreate takes panels first, then guide curves, and writes nothing unless every panel has an applicable guide. A guide counts for a bay only if it covers the whole bay; touching an endpoint does not count. Empty tracks are dropped until none remain, and since 2026-10-06 all new segments start unmerged.

**Why:** A mistaken selection would otherwise reset a panel to a 1x1 grid, and incidental contact created false offsets and dividers. Reading continuous guides as merges left new curves merged, which users did not want. Merges now come only from the editor, PCcrvTemplate or sync.

**Status:** current

### 2026-08-18 — Sync trusts geometry, not stored ids or grid indices

**Decision:** PCsyncSrf and PCsyncCrv match surfaces and curves to panels by position (geometric coincidence) and treat stale PID/CID values as fixable. They also line up old and new H/V tracks by geometry, so topology and curve names follow the tracks that remain; curves are renamed, not respawned. A panel with any ambiguity is skipped whole and left selected while the others continue.

**Why:** Hand-edited geometry must be usable as the authority for the layout. Resetting topology after a small surface split threw away unrelated design work. One bad panel must not block a large selection.

**Status:** current

### 2026-08-18 — Registered commands are user-facing only, with stable GUIDs

**Decision:** No test or smoke command is registered; validation lives in `tests/`. A rename changes only `EnglishName`, never the command class or its `[Guid]`, and adds no alias. Since 2026-10-31, names are `PC` plus camelCase (`PCeditor`, `PCmatchSrf`, `PCtemplatePNL`).

**Why:** Rhino shows every registered command in autocomplete, and aliases would keep the old names registered. Rhino command lookup ignores case, so the casing change did not break any macros.

**Status:** current

### 2026-08-18 — Topology as binary segment, merge and hide masks

**Decision:** Panel topology was stored as versioned Base64 bit masks. Each mask had a header with the grid size. The masks were later stored only when not default and renumbered to `CW_2.10_MERGE_MASK`, `CW_2.11_HIDE_MASK` and `CW_2.12_DELETE_MASK`.

**Why:** The header stopped a mask from being applied to a grid of a different size. At the user's request the masks became one combined configuration on 2026-10-07 and then readable text. The mask keys are now read only as a legacy fallback.

**Status:** superseded by "Frame configuration is readable text on the panel plus a code"

### 2026-08-19 — A blank cell is stored as a single space

**Decision:** Unassigned cells, and the blank placeholder keys that PCpid adds, are written as `" "` (`PanelCladdingKeyService.PersistedBlankCellValue`). Every reader trims the value.

**Why:** Rhino deletes a user-text key whose value is set to an empty string, which would drop the cell from the layout.

**Status:** current

### 2026-08-19 — Hide is distinct from delete

**Decision:** A hidden segment keeps the cells on both sides separate but produces no extrusion. A deleted segment merges those cells. If every segment of a track is deleted, the track is removed and later rows or columns are relabelled. Hidden segments never cause that removal, and merges cannot cross hidden or deleted segments.

**Why:** A material boundary can need separate cells with no frame member behind it. Treating it as a deletion would merge or renumber cells. A track with every segment deleted had left phantom grid lines and dimensions.

**Status:** current

### 2026-08-19 — Surface and curve commands are independent families

**Decision:** Surface work (`PCsyncSrf`, the Surfaces scope of `PCbake`) and curve work (`PCsyncCrv`, `PCmatchCrv`, the Curves scope) neither need nor write the other family. Surface sync on its own never removes a structural H/V track without proof; see the `Merge_Mark` entry.

**Why:** Users must be able to regenerate or reconcile one family alone. Surface geometry cannot tell an obsolete divider from a mullion that one cladding piece spans on purpose.

**Status:** current

### 2026-08-19 — PCmatchSrf copies the source cell graph exactly, and nothing else

**Decision:** PCmatchSrf writes each source cell's stored value exactly, so references stay references. It writes only cell assignments and keeps the target's offsets, frame configuration, IDs and other keys. It overwrites targets that already have assignments, and it reads every write back before accepting the batch.

**Why:** The first match command (2026-08-05) copied offsets, but offsets describe the target's own layout. A later version rewrote the source graph into a standard form and silently replaced the user's choice of owner cell. Refusing targets that already had assignments meant a half-written target could never be repaired. A successful `ModifyAttributes` call did not prove that the writes had landed.

**Status:** current

### 2026-08-19 — Cladding logic as a material-free hint

**Decision:** `CW_2.08_CLADDING_LOGIC` held only owner and reference labels as JSON. It was derived from the `CW_4.xx` cell keys, was explicitly not a source the user edits, and let sync keep the user's choice of owner cell.

**Why:** Without it, sync always chose the top-left cell as owner. At the user's request the logic key later became the single source, materials included.

**Status:** superseded by "`UD_CLADDING_LOGIC` is the only cladding read source"

### 2026-08-20 — The panel owns extrusion assignments

**Decision:** Frame and extrusion profile assignments are a versioned JSON payload on the panel (now `UD_FRAME_ASSIGNMENTS`), and baked curves only carry copies. Merging segments combines their codes, deleting a segment drops its codes, and formula text such as `LL` or `(LL+mod)*n` is kept exactly as written.

**Why:** One continuous baked curve must carry one unambiguous take-off set, while the panel stays the place users edit. Take-off reads the formula text, so its exact format must not change.

**Status:** current

### 2026-08-20 — Skia native DLLs sit beside the RHP

**Decision:** The package build copies `libSkiaSharp.dll` and `libHarfBuzzSharp.dll` into the plug-in folder itself, as well as into `runtimes/win-x64/native`. PDF rendering uses `PdfPig.Rendering.Skia`.

**Why:** When Rhino loads a plug-in it does not find native DLLs in nested runtime folders, and this failed in the field with `0x8007007E`. PdfPig with Skia needs no external Poppler or Python.

**Status:** current

### 2026-08-25 — `Merge_Mark` is the only proof that keeps a track

**Decision:** Every baked cladding surface records the cells it covers in `Merge_Mark`, owner first (`0A;1A`). In PCsyncSrf, a stored H/V track with no matching surface edge survives only if a complete, valid set of `Merge_Mark` values shows one surface spanning it. The key replaced `CW_1.03_CLADDING_CELLS`, which is read only to migrate old documents.

**Why:** Keeping every stored offset doubled tracks when surfaces moved. The editor's owner graph cannot prove which cells a surface actually covers. Marking only merged surfaces would leave the unmarked ones ambiguous. The `CW_1.03` prefix wrongly suggested a panel key.

**Status:** current

### 2026-08-26 — The installer writes the full registration; Rhino then owns root and CommandList

**Decision:** The installer writes the complete HKCU record: root identity, `LoadMode=1`, `DirectoryInstall=0`, `PlugIn\FileName` and the exact `CommandList`. It rejects the short form with a `FileName` value on the root key. Once Rhino loads the plug-in, it rewrites the root and `CommandList`, but `PlugIn\FileName` stays as the installer wrote it.

**Why:** Rhino 8 ignored the short first-load form and never loaded the RHP, yet a mix of both forms passed the old validator. `PlugIn\FileName` never repairs itself, so only `-Mode Validate` and the timestamp check catch a stale path there.

**Status:** current

### 2026-09-03 — Installer self-validation is not proof of installation

**Decision:** A production install counts only when two checks pass:
- A fresh process, started after the installer exits, reads the real registry and finds the new RHP and the exact `CommandList`.
- The next Rhino start loads that RHP.

Until then the previous RHP stays in place. The agent's sandboxed shell does not install; installs run from the user's Terminal panel.

**Why:** Version 1.0.70 passed validation while the real registry still pointed at the removed 1.0.69 RHP, because the sandbox redirected the registry writes. It happened again on 2026-10-26 and left the commands unknown for several minutes.

**Status:** current

### 2026-09-03 — Managed scope: only objects under managed layer roots count

**Decision:** CID conflict checks and the discovery done by sync and bake consider only surfaces and curves under the managed layer roots, through one shared check. The same PID or CID elsewhere, such as in detail models or wireframe copies, is ignored.

**Why:** A search of the whole document let copies made for detail modelling block baking and get pulled into sync. Separate checks for spawn and sync had drifted apart.

**Status:** current (roots renamed by the 2026-10-23 layer standard)

### 2026-09-03 — Baking reconciles managed objects in place

**Decision:** For the selected panels, baking:
- updates the object for each expected CID in place, keeping its Rhino object id;
- creates missing objects and deletes managed objects of that PID whose CID is no longer expected;
- changes locked or hidden objects with `ignoreModes` and keeps their mode;
- skips groups of panels that share a CID;
- keeps old dependent objects that several panels could own, instead of assigning them by PID.

**Why:** Stable object ids protect references to those objects elsewhere. Lock and visibility only control display and editing, so they must neither block the update nor be reset silently. Parent and child panels share a PID, so ownership cannot be worked out from the PID alone.

**Status:** current (as `PCbake`)

### 2026-09-16 — PCcrvTemplate never overwrites configured merges

**Decision:** Panels that already have merge runs are skipped and reported, and the others get the template. A selection where every panel is configured succeeds without changing anything.

**Why:** The user asked that hand-configured merges never be overwritten and never block a batch. This replaced a rule that rejected the whole batch, which had itself replaced overwriting.

**Status:** current

### 2026-09-23 — The CID role suffix is derived from the PID

**Decision:** A panel's CID is rebuilt from the `CID-` form of its PID plus a role suffix (`_P`, `_C`, `_C1`), never by adding to the stored CID. The role now comes from the unit type (`CW_1.06_UNIT_TYPE`: C, F, L or Z) and, for L and Z panels, from lowercase `parent`/`child` in `CW_5.00_DEFINITION_A`. If the unit type is not specified, the stored CID is kept.

**Why:** Rebuilding from the PID means re-runs and role changes cannot stack suffixes. An explicit flat or curved type must remove a stale suffix, so "no suffix" has to be different from "not specified".

**Status:** current

### 2026-09-30 — User key renames ship with a document migration

**Decision:** When the user renames attribute keys, live documents are migrated together with the new plug-in version, and older builds are not run on them. `CW_1.05_RELEASE` became `CW_1.05_LOT` with no fallback to the old key. Lot values are copied to dependent objects as trimmed raw text and never trigger geometry changes.

**Why:** The user owns the attribute schema, and an older RHP would recreate old keys beside the new ones. Lots are identifiers, so leading zeros and text must survive.

**Status:** current

### 2026-09-30 — Cladding type identity suspended, code kept dormant

**Decision:** The derived cladding type code and signature are no longer generated, read or written. Saves, and syncs that change a panel, delete the retired keys (`CW_2.14_CLADDING_TYPE` and its earlier names, `CW_4.00_*`, `Signature`). The identity and workbook code stays in place for a redesign.

**Why:** The user is reconsidering what a combined identity should contain, and a stale hash would mislead. Two lessons for the redesign:
- The short code was never the identity; a full versioned SHA-256 signature was stored.
- An identity based on materials only broke once merged regions existed.

**Status:** current

### 2026-10-07 — PCpid numbers from user-picked references and infers nothing

**Decision:** PCpid takes north from the outward normal of a north panel the user picks, and levels from bottom edges relative to a first-floor panel the user picks. It takes bays from aligned left edges and never fills in missing floors or bays. It reads the whole panel-layer tree for context, writes only the selected panels, and rejects any duplicate PID in that tree.

**Why:** A guess based on the building's centroid mislabelled a set-back facade. Panels of different heights must be compared by their bottom edges. Before PCpid read the whole tree, the numbers changed with the size of the selection.

**Status:** current

### 2026-10-07 — Point order is fixed inside the command; curved panels are never flattened

**Decision:** PCpid and baking normalise four-sided panel surfaces in this order: lower-left start corner seen from the front, clockwise corners, four-point rebuild, front/back flip, then `SwapUV`. This happens inside the command's own Undo record and before dependent objects are generated. Curved, non-four-sided, holed or degenerate panels are reported and skipped.

**Why:** A separate scripted pass would split the work across two Undo records. The rebuild changes the surface's UV parameters, so generation must come after it. Flattening, or taking corners from a bounding box, would change real geometry.

**Status:** current

### 2026-10-14 — Releases are per-user and immutable

**Decision:** A release is a zip with a SHA-256 checksum that installs to LocalAppData and HKCU without elevation. The installer refuses a registration that belongs to another product before moving any file, and restores the previous HKCU export if installation fails. Published assets and release notes are never replaced. A fix is a new version, and the `vX.Y.Z` tag goes on the commit that built the package.

**Why:** A release test found a path, after the installer refused a repair, that could delete another product's registration. The installer decides replacement and rollback by version number. The RHP product version embeds the commit it was built from (`1.2.5+<commit>`).

**Status:** current

### 2026-10-23 — IDs use `-` inside an identifier and `_` between components

**Decision:** IDs look like `PID-ABC-N2-05-05` and `CID-ABC-N2-05-05`, with roles `_P`/`_C`, cells `_0A` and curves `_FRM-0`. The retired `PID_…` form is not read as an ID. Since 2026-10-06, Rhino object names are the CID without `CID-<project>-` and are overwritten on every plug-in write. The CID itself is never changed to produce a name.

**Why:** This was the user's decision. Accepting both forms would let mixed IDs exist in one document. Documents were migrated by swapping the two characters, which converts every old ID exactly. Object names must follow the ID through every command.

**Status:** current

### 2026-10-23 — Project layer standard

**Decision:** Each kind of object has a fixed layer:
- Panels: `<CODE>-IEF-Wireframe::Surfaces - PNL::Surfaces - <type>`
- Curves: `<CODE>-IEF-Wireframe::Curves - PNL::Curves - <type>`
- Cladding surfaces: `Reference::Surfaces - STEP::Surfaces - <prefix>::<material>`

The project code comes from the file name (`ABC - Wireframe.3dm`). Older root layers are no longer recognised, and PCreport flags them.

**Why:** The project wireframe holds copies with the same PID and CID, and these must stay out of conflict checks and sync. Any project must work without configuration.

**Status:** current

### 2026-10-23 — Frame configuration is readable text on the panel plus a code

**Decision:** Planners write the readable configuration (`2H 1V | delete … | merge … | hide …`) to `UD_FRAME_CONFIG`. Live writes derive a two-letter `CW_1.08_FRAME_CONFIG` code from it through the naming sheet. Codes skip I and O and are never reassigned or reused, and change detection ignores the derived code.

**Why:** These were the user's decisions. Keeping the full text on each panel means layouts still read and load when the sheet is locked or missing. A code without its text fails with `PANEL_FRAME_CONFIG_BACKUP_MISSING` rather than guessing.

**Status:** current

### 2026-10-23 — The naming sheet is written on save and never blocks it

**Decision:** After each save of the main model (named exactly `<CODE> - Wireframe.3dm`), the plug-in updates only its own columns of `<CODE> - Naming Convention.xlsx`. It edits a copy and moves it over the original. A locked sheet only produces a warning, and the next save catches up; if nothing changed, nothing is written. Since 2026-10-26, other models read the sheet but do not write it.

**Why:** `File.Replace` needs a backup file, and the project share and a synced cloud drive keep hold of that backup. Several models share one sheet, and saving a secondary model changed which codes were marked used or unused.

**Status:** current

### 2026-10-26 — Per-project code format in the workbook; assigned codes never move

**Decision:** The code format for the PNL sheet is stored in the workbook custom property `PanelCladdingEditor.PNLFormat` (tokens `AA`, `A`, `A1`, `01`, `001`). Sync updates the sheet in place:
- A code keeps its row, and new codes take the first free row.
- Descriptions that are not empty are never rewritten; differences are reported as conflicts.
- Unused codes are struck through.
- Rows are never cleared or reordered.

**Why:** A property inside the workbook is shared by every model of the project, survives editing in Excel, and uses no cell the team edits. An earlier sync that rewrote the sheet dropped placeholder rows and turned numbers into text.

**Status:** current

### 2026-10-27 — Auto attributes are always on and stay outside Undo

**Decision:** On every panel in the `Surfaces - PNL` branch the plug-in keeps these values current:
- unit width, height and dimension (measured in the panel's own frame, five decimals, `<w>x<h>`)
- area (`F3` plus ` sqf`) and weight (PSF × area)
- `UD_PLANE` (object user text, `origin;X;Y`), which replaced the `Plane` key

It writes when Rhino is idle, outside Undo, only for keys that changed, and never while an Undo is running.

**Why:** This replaced the standalone AutoATT script. A world-aligned bounding box gives rotated facades too large a width. Writing only differences ends event loops, and Undo restores values that belong to the old geometry. The old code read `Plane` from the wrong place, so stored planes were never used.

**Status:** current

### 2026-10-27 — `UD_` keys for plug-in payloads, migrated lazily

**Decision:** Payloads the plug-in writes use `UD_` keys (`UD_CLADDING_LOGIC`, `UD_FRAME_CONFIG`, `UD_FRAME_ASSIGNMENTS`). Reads fall back to the legacy key, and the next plug-in write to a panel renames its legacy key. There is no model-wide migration: legacy and retired keys are cleaned only on panels an operation touches. Conflicting case variants, or a current value that does not parse, fail instead of falling back.

**Why:** The user changed the naming convention. Old models must keep loading and migrate gradually without mass-editing panels nobody touched; the same rule retired `Signature` on 2026-08-19. A silent fallback would hide corrupt data. The logic key went from `CW_2.08` to `CW_2.13`, `CW_9.01` and then `UD_CLADDING_LOGIC`.

**Status:** current

### 2026-10-27 — `UD_CLADDING_LOGIC` is the only cladding read source

**Decision:** Cell materials are read only from `UD_CLADDING_LOGIC`: owner cells hold the material, and member cells hold the owner's label. The `CW_4.xx_CLADDING_*` keys are written but never read. A panel without cladding configured loads from H/V alone, and PCcreate never writes `CW_4` keys. A configured panel whose logic no longer fits its grid fails with `PANEL_CLADDING_LOGIC_GRID_MISMATCH`.

**Why:** This was the user's decision: until the user configures cladding, loading depends only on H/V, and the plug-in must not generate or overwrite cladding data. The accepted cost is that hand edits to `CW_4` keys are ignored and overwritten on the next save.

**Status:** current

### 2026-10-27 — Hand-filled keys belong to the user

**Decision:** The plug-in never writes or deletes `CW_1.09_FRAME_TYPE` and reads it only for the naming sheet; assignment payloads moved to `UD_FRAME_ASSIGNMENTS`. PCpid adds missing setup keys as `" "` placeholders but never overwrites a key that already exists, in any letter case.

**Why:** Users fill in `CW_1.09` by hand. The plug-in had rejected their values as invalid payloads, and PCcreate deleted them. Placeholders give users the full set of keys without inventing business values.

**Status:** current

### 2026-10-27 — Yak is a second, exclusive install channel

**Decision:** `Build-PanelCladdingEditorYak.ps1` packages the same verified files as the zip. It publishes only to a company source the user names and refuses `yak.rhino3d.com`; a folder source gets a file copy instead of `yak push`. A machine uses the zip or Yak, never both, and switching to Yak starts with the zip's `Uninstall.cmd`.

**Why:** The user chose Rhino Package Manager over writing a self-update command. With both channels installed, Rhino would find the same GUID twice, and the zip installer moves Package Manager copies aside anyway. `yak push` to a folder demands a Rhino Accounts token.

**Status:** current

### 2026-10-28 — Tests live in `tests/`, and no failure is accepted

**Decision:** All rerunnable test code sits under `tests/` with one runner, `tests/Run-Regression.ps1`:
- It builds one project at a time (`-m:1`) into a shared `artifacts/tests/bin/<Configuration>/`, and each project keeps its own `obj`.
- Runs never write files tracked by git.
- A stale smoke test is updated, never accepted as a known failure.
- Probes that need RhinoCommon's native code report `[SKIP]` outside Rhino.

**Why:**
- Test code kept beside dated evidence drifted from it.
- Test runs rewrote committed images.
- Per-project output grew to about 16 GB.
- Five "accepted" failures hid real regressions.
- The RHP and the test DLL build from one project and clashed when built in parallel.
- Moving `obj` breaks WPF markup compilation.
- Rhino's native engine does not load outside Rhino.

**Status:** current

### 2026-10-31 — Excluded layers stay out of tracking but keep auto values

**Decision:** A layer segment containing "Not in Scope" or "By Others" (ignoring case, spaces, `-` and `_`) takes its panels out of PCpid, the naming sync, PCreport and curve routing. Their values are left alone, and their PIDs still count as duplicates. Auto attributes still refresh on them (`IsPanelBranchPath` versus `IsPanelLayerPath`).

**Why:** The user decided such layers need no tracking, and refusing a duplicate PID is better than creating one. In live tests, stale sizes on excluded panels hid the real error, and some data is better than none.

**Status:** current

### 2026-10-31 — Extrusion assignment UI suspended behind a switch

**Decision:** `_extrusionAssignmentEnabled = false` in `PanelCladdingEditorWindow` hides the assignment drop zone and the profile catalogue. The assignment code and the stored `UD_FRAME_ASSIGNMENTS` remain and still drive match, sync and bake.

**Why:** The user asked to remove assignment temporarily, and the switch keeps that reversible. This is not dead code to delete.

**Status:** current

### 2026-10-31 — PCbake replaces the update and spawn commands

**Decision:** `PCbake` replaced `PCupdate`, `PCspawnSrf` and `PCspawnCrv`. Its scope is Surfaces by default on every run, or Curves or All, and kinds outside the scope are untouched. A panel that already owns any managed curve keeps all its curves: nothing is rebuilt, deleted or added.

**Why:** "Bake" names the direction, attributes into geometry, which is the opposite of sync; the spawn commands duplicated what update already did. Existing curves carry hand work that a rebuild would lose while assignment is suspended. To re-bake curves, delete them first.

**Status:** current

### 2026-10-31 — PCreport is read-only with fixed rules

**Decision:** `PCreport` writes nothing, opens no Undo record and offers no automatic fixes. Its rules are fixed in code and reuse the plug-in's own checks. It examines only objects on managed layers and reports missing keys as Info. Retired layer names may appear in the source only in its `RetiredRootLayers` list.

**Why:** These were the user's decisions. A rule that also checked unmanaged objects flooded a live model with rows for wireframe copies that must keep their PID, and it was removed.

**Status:** current

### 2026-11-01 — PCweb starts the launcher through the shell

**Decision:** `PCweb` depends only on the path to the Wireframe Web launcher (an HKCU value or the default) and on the launcher's arguments, output files and exit codes. It starts the launcher with `UseShellExecute = true`, reads results from files, and waits off the UI thread for at most three minutes.

**Why:** With redirected stdout the launcher inherits Rhino's handles, including the lock on the open model. The browser it starts outlives it, and later saves fail with "File Is Read-only". Do not switch back to redirected streams.

**Status:** current

### 2026-11-04 — Fictional project codes are three letters

**Decision:** Examples and tests use `ABC` and `XYZ`, with `QRS` and `JKL` where a further project is needed. `MET` is the metal material category, not a project code, and must stay.

**Why:** The repository is public and once held client data. PCpid accepts only three-letter project codes, so longer placeholders broke smoke tests. A blind replacement of `MET` once broke the material category.

**Status:** current

### 2026-11-05 — Client-data guard in local hooks with a private denylist

**Decision:** The hooks in `.githooks` (`pre-commit`, `commit-msg`, `pre-push`), enabled by `tests/Run-Regression.ps1`, run `Test-RepositoryContent.ps1`. It checks against `content-rules.json` and against a list of real client names kept on each machine outside the repository (`%APPDATA%\PanelCladding\content-denylist.txt`). Reports give the file, line and rule but never the matched text. Live evidence goes to `%LOCALAPPDATA%\PanelCladding\LiveEvidence`, and `PcLive` refuses any folder inside a git working tree.

**Why:** Most leaked files were dumps from live runs written into tracked folders. A smoke test over committed files catches leaks too late. A list of real clients inside a public repository would itself be a leak.

**Status:** current

### 2026-11-06 — Living guides instead of per-change records

**Decision:** Changes no longer write PLAN, EXET or TEST records. Each change updates the
architecture, operations and decision guides, the package README and the release notes it affects,
and its pull request description records what changed and how it was validated. The former records
are kept at tag `records-2026-11-06`.

**Why:** The records had grown larger than the source code (about 43,000 lines), nearly every commit
had to touch them, and work stayed open in them because later verification was never written back.
The knowledge later work needs was spread over hundreds of dated files while the guides stayed thin,
and dated evidence folders were where client data had leaked.

**Status:** current

### 2026-11-06 — Each checkout runs its own client-data hooks

**Decision:** Git must take the hooks from the checkout's own `.githooks` folder. The Claude desktop
app writes the main checkout's absolute `.githooks` path as `core.hooksPath` into the
`config.worktree` of each worktree it creates; `tests/Run-Regression.ps1` replaces exactly that shape
(worktree scope, another checkout's `.githooks`) with `git config --worktree core.hooksPath .githooks`
and only warns about any other hooks folder. The `repository-content-guard` smoke accepts any
`core.hooksPath` that resolves to the checkout's own `.githooks` and fails on anything else.

**Why:** With the main checkout's path, a worktree's commits and pushes are checked by whatever branch
the main checkout has checked out: a branch's own rule changes are not exercised, and no hook runs
at all if that branch has no `.githooks`. A relative `.githooks` works in every worktree because git
runs hooks from the working tree root. Changing only that one shape in the worktree's own config
leaves a deliberately chosen hooks manager alone.

**Status:** current
