// PanelCladdingEditor live acceptance checks (1.3.0 manual test list, sections 1-5).
//
// Runs inside Rhino 8 against the open test model through a C# script host that supplies the
// document, a JSON parameter object and a log callback, for example MCP_Rhino's live scripting
// with libraryPath = this file and libraryType = PcLive:
//
//   Result = PcLive.Start(doc, Parameters, Log);   // queues one suite and returns at once
//   Result = PcLive.Read(Parameters);               // returns the suite's results so far (JSON)
//
// Parameters: { "suite": "commands|bake|excluded|report|scan|export|editor|otherdocs|web|probe" (several
// joined by '+'), "evidenceDir": "<folder>", and "mainModel" for otherdocs, "assembly" for web, "macro"
// for probe }.
// A suite is a list of steps run one per RhinoApp.Idle tick, outside the host's own undo record,
// the way typed commands run. Commands are sent with RhinoApp.RunScript; objects are picked with
// a preselection or the transparent '_SelID command. No keyboard or mouse input is generated.
// Every file name used by this class is fully qualified because the host strips using directives.
public static class PcLive
{
    private const string StatePrefix = "PcLive.";
    private static readonly Guid PlugInId = new Guid("7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35");
    private static readonly string[] ExpectedCommands =
    {
        "PCeditor", "PCcreate", "PCclear", "PCcrvTemplate", "PCmatchSrf", "PCmatchCrv",
        "PCbake", "PCsyncSrf", "PCsyncCrv", "PCpid", "PCreport", "PCtemplatePNL", "PCweb"
    };
    private static readonly string[] RemovedCommands = { "PCupdate", "PCspawnSrf", "PCspawnCrv" };

    private const string Root = "XYZ-IEF-Wireframe";
    private const string PanelBranch = Root + "::Surfaces - PNL";
    private const string CurveBranch = Root + "::Curves - PNL";
    private const string SurfaceBranch = "Reference::Surfaces - STEP";
    private const string TestPanelLayer = PanelBranch + "::PNL-EXT-3A.1";
    private const string PidKey = "CW_1.01_PID";
    private const string CidKey = "CW_1.02_CID";
    private const string LogicKey = "UD_CLADDING_LOGIC";
    private const string FrameAssignmentsKey = "UD_FRAME_ASSIGNMENTS";

    public static object Start(RhinoDoc doc, JsonElement parameters, Action<string> log)
    {
        string suite = Text(parameters, "suite");
        string evidence = Text(parameters, "evidenceDir");
        // "commands+bake" runs both suites in order as one run.
        var steps = new List<Step>();
        foreach (string name in suite.Split('+'))
        {
            switch (name)
            {
                case "commands": steps.AddRange(CommandSteps()); break;
                case "bake": steps.AddRange(BakeSteps()); break;
                case "excluded": steps.AddRange(ExcludedSteps()); break;
                case "report": steps.AddRange(ReportSteps()); break;
                case "scan": steps.AddRange(ReportSteps().Take(2)); break;   // open, read-only check and rule dump only
                case "export": steps.AddRange(ExportSteps()); break;
                case "editor": steps.AddRange(EditorSteps()); break;
                case "otherdocs": steps.AddRange(OtherDocumentSteps()); break;
                case "web": steps.AddRange(WebSteps()); break;
                case "probe": steps.AddRange(ProbeSteps()); break;
                default: throw new ArgumentException("Unknown suite: " + name);
            }
        }
        var runner = new Runner(new Ctx(suite, doc.RuntimeSerialNumber, evidence, parameters), steps);
        runner.Begin();
        return new { suite, steps = steps.Select(step => step.Name).ToArray() };
    }

    public static object Read(JsonElement parameters) =>
        AppDomain.CurrentDomain.GetData(StatePrefix + Text(parameters, "suite")) as string ?? "not started";

    // ---------------------------------------------------------------- 1. Command names

    private static List<Step> CommandSteps() => new List<Step>
    {
        new Step("1.0 loaded plug-in", ctx =>
        {
            System.Reflection.Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(item => item.GetName().Name == "PanelCladdingEditor");
            string location = assembly?.Location ?? string.Empty;
            string hash = string.Empty;
            if (System.IO.File.Exists(location))
            {
                using (var stream = System.IO.File.OpenRead(location))
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            }
            string version = assembly?.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? string.Empty;
            ctx.Note("1.0 loaded plug-in", new { process = System.Diagnostics.Process.GetCurrentProcess().Id, rhino = RhinoApp.Version.ToString(), location, sha256 = hash, version });
            ctx.Check("1.0 Rhino runs the installed 1.3.0 RHP", version.StartsWith("1.3.0+150f546") &&
                location.EndsWith(@"PanelCladdingEditor\plugin\1.3.0\PanelCladdingEditor.rhp", StringComparison.OrdinalIgnoreCase),
                new { location, hash, version });
            return true;
        }),
        new Step("1.1 command registry", ctx =>
        {
            string[] pluginNames = Rhino.PlugIns.PlugIn.GetEnglishCommandNames(PlugInId) ?? new string[0];
            string[] loaded = Rhino.Commands.Command.GetCommandNames(true, true)
                .Where(name => name.StartsWith("PC", StringComparison.OrdinalIgnoreCase)).OrderBy(name => name).ToArray();
            string[] all = Rhino.Commands.Command.GetCommandNames(true, false)
                .Where(name => name.StartsWith("PC", StringComparison.OrdinalIgnoreCase)).OrderBy(name => name).ToArray();
            string[] aliases = Rhino.ApplicationSettings.CommandAliasList.GetNames()
                .Where(name => name.StartsWith("PC", StringComparison.OrdinalIgnoreCase)).ToArray();
            bool exact = new HashSet<string>(pluginNames, StringComparer.Ordinal).SetEquals(ExpectedCommands);
            ctx.Check("1.1 plug-in registers exactly the " + ExpectedCommands.Length + " commands", exact, pluginNames);
            ctx.Check("1.1 'PC' completion lists only these " + ExpectedCommands.Length,
                all.All(name => ExpectedCommands.Contains(name, StringComparer.OrdinalIgnoreCase)) && all.Length == ExpectedCommands.Length,
                new { loadedCommandsStartingWithPC = loaded, registeredCommandsStartingWithPC = all, aliasesStartingWithPC = aliases });
            return true;
        }),
        new Step("1.2 old spelling", ctx =>
        {
            string editor = ctx.Cmd("_PCEditor _Enter");
            ctx.Check("1.2 PCEditor runs PCeditor", editor.Contains("Select one panel Brep to edit") && !editor.Contains("Unknown command"), editor);
            string bake = ctx.Cmd("_PCBAKE _Enter");
            ctx.Check("1.2 PCBAKE runs PCbake", bake.Contains("Select panel Breps to bake") && !bake.Contains("Unknown command"), bake);
            return true;
        }),
        new Step("1.3 removed commands", ctx =>
        {
            foreach (string name in RemovedCommands)
            {
                string history = ctx.Cmd("_" + name);
                ctx.Check("1.3 " + name + " is unknown", history.Contains("Unknown command"), history);
            }
            return true;
        }),
        new Step("1.4 aliases and toolbars", ctx =>
        {
            var aliasHits = Rhino.ApplicationSettings.CommandAliasList.ToDictionary()
                .Where(pair => RemovedCommands.Any(name => pair.Value.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0))
                .Select(pair => pair.Key + " = " + pair.Value).ToArray();
            var files = new List<object>();
            var toolbarHits = new List<string>();
            foreach (Rhino.UI.ToolbarFile file in Rhino.RhinoApp.ToolbarFiles)
            {
                string path = file.Path ?? string.Empty;
                files.Add(new { file.Name, path });
                if (!System.IO.File.Exists(path)) continue;
                string text = System.IO.File.ReadAllText(path);
                foreach (string name in RemovedCommands)
                {
                    int count = System.Text.RegularExpressions.Regex.Matches(text, @"\b_?" + name + @"\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
                    if (count > 0) toolbarHits.Add(System.IO.Path.GetFileName(path) + ": " + name + " x" + count);
                }
            }
            ctx.Check("1.4 no local alias or loaded toolbar calls a removed command", aliasHits.Length == 0 && toolbarHits.Count == 0,
                new { aliasHits, toolbarHits, toolbarFiles = files });
            return true;
        }),
    };

    // ---------------------------------------------------------------- 2. PCbake

    private static readonly Dictionary<string, string> FourCells = new Dictionary<string, string>
    {
        ["0A"] = "MTL-001", ["0B"] = "GLS-001", ["1A"] = "MTL-001", ["1B"] = "GLS-001"
    };
    private static readonly Dictionary<string, string> TwoMergedCells = new Dictionary<string, string>
    {
        ["0A"] = "MTL-001", ["0B"] = "GLS-001", ["1A"] = "0A", ["1B"] = "0B"
    };

    private static List<Step> BakeSteps() => new List<Step>
    {
        new Step("2.0 setup", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            List<Rhino.DocObjects.RhinoObject> panels = PanelsOn(doc, TestPanelLayer);
            if (panels.Count < 3) throw new InvalidOperationException("Need 3 panels on " + TestPanelLayer);
            string[] names = { "A", "B", "C" };
            for (int i = 0; i < names.Length; i++)
            {
                Rhino.DocObjects.RhinoObject panel = panels[i];
                ctx.Bag["panel" + names[i]] = panel.Id;
                int removed = DeleteManaged(doc, panel);
                WriteLayout(doc, panel.Id, 0.5, 0.5, FourCells);
                ctx.Note("panel " + names[i], new { id = panel.Id, pid = Pid(panel), cid = Cid(panel), removedFromEarlierRun = removed });
            }
            doc.Objects.UnselectAll();
            doc.Views.Redraw();
            return true;
        }),
        new Step("2.1 preselected panel, default scope", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            Guid a = (Guid)ctx.Bag["panelA"];
            string curvesBefore = CurveFingerprint(doc);
            SelectOnly(doc, a);
            string history = ctx.Cmd("_-PCbake _Enter");
            var surfaces = ManagedSurfaces(doc, a);
            ctx.Check("2.1 prompt shows Scope=Surfaces", history.Contains("Scope=Surfaces"), history);
            ctx.Check("2.1 Enter bakes surfaces only", history.Contains("PCbake (Surfaces) processed 1 panel(s)") &&
                history.Contains("and 0 curve(s), updated") && surfaces.Count == 4, new { surfaces = surfaces.Count });
            ctx.Check("2.1 curves unchanged", CurveFingerprint(doc) == curvesBefore, null);
            ctx.Check("2.1 preselected panel stays selected", IsSelected(doc, a), null);
            ctx.Bag["surfacesA"] = SurfaceState(surfaces);
            return true;
        }),
        new Step("2.2 no preselection offers Scope; Curves", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            Guid b = (Guid)ctx.Bag["panelB"];
            doc.Objects.UnselectAll();
            string surfacesBefore = SurfaceFingerprint(doc);
            string history = ctx.Cmd("_-PCbake _Scope=_Curves '_SelID " + b + " _Enter");
            var curves = ManagedCurves(doc, b);
            ctx.Check("2.2 selection prompt offers Scope", history.Contains("Select panel Breps to bake ( Scope=Surfaces )"), history);
            ctx.Check("2.2 Scope=Curves processes curves only", history.Contains("PCbake (Curves) processed 1 panel(s): created 0 surface(s)") &&
                curves.Count > 0 && SurfaceFingerprint(doc) == surfacesBefore, new { curves = curves.Count });
            ctx.Bag["curveCountB"] = curves.Count;
            CheckCurveColours(ctx, "2.6", curves);
            ctx.Check("2.2 panel picked at the prompt is not left selected", !IsSelected(doc, b), null);
            return true;
        }),
        new Step("2.3 Scope=All", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            Guid c = (Guid)ctx.Bag["panelC"];
            doc.Objects.UnselectAll();
            string history = ctx.Cmd("_-PCbake _Scope=_All '_SelID " + c + " _Enter");
            int surfaces = ManagedSurfaces(doc, c).Count, curves = ManagedCurves(doc, c).Count;
            ctx.Check("2.3 Scope=All processes surfaces and curves", history.Contains("PCbake (All) processed 1 panel(s)") &&
                surfaces == 4 && curves > 0, new { history, surfaces, curves });
            return true;
        }),
        new Step("2.4 next run starts at Surfaces", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            Guid a = (Guid)ctx.Bag["panelA"];
            SelectOnly(doc, a);
            string history = ctx.Cmd("_-PCbake _Enter");
            ctx.Check("2.4 the run after Scope=All starts at Scope=Surfaces", history.Contains("( Scope=Surfaces )") &&
                history.Contains("PCbake (Surfaces) processed 1 panel(s)"), history);
            doc.Objects.UnselectAll();
            return true;
        }),
        new Step("2.5 panel with curves keeps them", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            Guid b = (Guid)ctx.Bag["panelB"];
            List<Rhino.DocObjects.RhinoObject> curves = ManagedCurves(doc, b);
            // Hand edits: move one curve, give it an assigned extrusion and a colour.
            Guid edited = curves[0].Id;
            doc.Objects.Transform(edited, Rhino.Geometry.Transform.Translation(0, 0, 1.5), true);
            Rhino.DocObjects.RhinoObject moved = doc.Objects.FindId(edited);
            var attributes = moved.Attributes.Duplicate();
            attributes.SetUserString("Extrusions", "1D-LIVE-TEST");
            attributes.SetUserString("1D-LIVE-TEST", "L");
            attributes.ObjectColor = System.Drawing.Color.FromArgb(255, 10, 20, 30);
            attributes.ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject;
            doc.Objects.ModifyAttributes(moved, attributes, true);
            string before = CurveState(ManagedCurves(doc, b));
            SelectOnly(doc, b);
            string curvesRun = ctx.Cmd("_-PCbake _Scope=_Curves _Enter");
            string afterCurves = CurveState(ManagedCurves(doc, b));
            SelectOnly(doc, b);
            string allRun = ctx.Cmd("_-PCbake _Scope=_All _Enter");
            string afterAll = CurveState(ManagedCurves(doc, b));
            ctx.Check("2.5 Curves leaves every curve of the panel unchanged", afterCurves == before, new { edited });
            ctx.Check("2.5 All leaves every curve of the panel unchanged", afterAll == before, null);
            ctx.Check("2.5 command line reports kept curves", curvesRun.Contains("Kept the existing curves of 1 panel(s)") &&
                allRun.Contains("Kept the existing curves of 1 panel(s)"), new { curvesRun, allRun });
            doc.Objects.UnselectAll();
            return true;
        }),
        new Step("2.7 curves are baked again after deleting them", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            Guid b = (Guid)ctx.Bag["panelB"];
            foreach (var curve in ManagedCurves(doc, b)) doc.Objects.Delete(curve.Id, true);
            SelectOnly(doc, b);
            string history = ctx.Cmd("_-PCbake _Scope=_Curves _Enter");
            var curves = ManagedCurves(doc, b);
            ctx.Check("2.7 Curves creates them again", curves.Count == (int)ctx.Bag["curveCountB"] &&
                !history.Contains("Kept the existing curves"), new { history, curves = curves.Count });
            doc.Objects.UnselectAll();
            return true;
        }),
        new Step("2.8 changed cladding rebuilds; one undo", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            Guid a = (Guid)ctx.Bag["panelA"];
            string beforeBake = SurfaceState(ManagedSurfaces(doc, a));
            WriteLayout(doc, a, 0.5, 0.5, TwoMergedCells);
            SelectOnly(doc, a);
            uint undoBefore = doc.NextUndoRecordSerialNumber;
            string history = ctx.Cmd("_-PCbake _Enter");
            uint undoAfter = doc.NextUndoRecordSerialNumber;
            int after = ManagedSurfaces(doc, a).Count;
            ctx.Check("2.8 default run rebuilds the surfaces and deletes the extra ones", after == 2 &&
                System.Text.RegularExpressions.Regex.IsMatch(history, @"updated [1-9]\d* surface") &&
                history.Contains("deleted 2 obsolete"), new { history, surfacesAfter = after });
            doc.Objects.UnselectAll();
            // Undo only a run that succeeded; a failed run has already rolled itself back. The Undo
            // command is used (as Ctrl+Z does): RhinoDoc.Undo() called outside a command left the
            // document with an undo record that never closed.
            string undo = history.Contains("PCbake (Surfaces) processed") ? ctx.Cmd("_Undo") : string.Empty;
            string afterUndo = SurfaceState(ManagedSurfaces(doc, a));
            ctx.Check("2.8 one undo restores the whole run", undo.Contains("Undoing PCbake") && afterUndo == beforeBake,
                new { undoRecordsUsedByRun = undoAfter - undoBefore, undo });
            // Leave the panel consistent again.
            WriteLayout(doc, a, 0.5, 0.5, FourCells);
            return true;
        }),
        new Step("2.9 locked and hidden surfaces", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            Guid c = (Guid)ctx.Bag["panelC"];
            var surfaces = ManagedSurfaces(doc, c);
            Guid locked = surfaces[0].Id, hidden = surfaces[1].Id;
            doc.Objects.Lock(locked, true);
            doc.Objects.Hide(hidden, true);
            string before = SurfaceState(ManagedSurfaces(doc, c));
            WriteLayout(doc, c, 0.4, 0.5, FourCells);
            SelectOnly(doc, c);
            string history = ctx.Cmd("_-PCbake _Enter");
            var lockedNow = doc.Objects.FindId(locked);
            var hiddenNow = doc.Objects.FindId(hidden);
            string after = SurfaceState(ManagedSurfaces(doc, c));
            ctx.Check("2.9 locked and hidden surfaces are updated", after != before && lockedNow != null && hiddenNow != null &&
                history.Contains("updated 4 surface"), new { history });
            ctx.Check("2.9 they stay locked and hidden", lockedNow != null && lockedNow.IsLocked && hiddenNow != null && hiddenNow.IsHidden,
                new { locked, hidden });
            doc.Objects.UnselectAll();
            return true;
        }),
    };

    private static void CheckCurveColours(Ctx ctx, string id, List<Rhino.DocObjects.RhinoObject> curves)
    {
        var kinds = curves.GroupBy(CurveKind)
            .ToDictionary(group => group.Key, group => group.Select(curve => ColorText(curve.Attributes)).Distinct().ToArray());
        bool pass = kinds.ContainsKey("frame") && kinds.ContainsKey("horizontal") && kinds.ContainsKey("vertical") &&
            kinds["frame"].SequenceEqual(new[] { "0,0,255" }) && kinds["horizontal"].SequenceEqual(new[] { "128,0,128" }) &&
            kinds["vertical"].SequenceEqual(new[] { "0,100,0" });
        ctx.Check(id + " curves are coloured by type", pass, new
        {
            kinds,
            codes = curves.Select(curve => curve.Attributes.GetUserString("CRV")).ToArray(),
            sources = curves.Select(curve => curve.Attributes.ColorSource.ToString()).Distinct().ToArray()
        });
    }

    // Frame curves carry FRM-n codes; intermediate ones are told apart by direction (the test panels are walls).
    private static string CurveKind(Rhino.DocObjects.RhinoObject curve)
    {
        if ((curve.Attributes.GetUserString("CRV") ?? string.Empty).StartsWith("FRM", StringComparison.OrdinalIgnoreCase)) return "frame";
        var geometry = (Rhino.Geometry.Curve)curve.Geometry;
        Rhino.Geometry.Vector3d direction = geometry.PointAtEnd - geometry.PointAtStart;
        return Math.Abs(direction.Z) < 0.1 * direction.Length ? "horizontal" : "vertical";
    }

    // ---------------------------------------------------------------- 3. Not in Scope / By Others

    private const string OriginLayerKey = "PCLIVE_ORIGIN_LAYER";
    private const string NewPanelKey = "PCLIVE_NEW_PANEL";
    private static readonly string[] ExcludedNames = { "Surfaces - Not in Scope", "Surfaces - By Others", "NOT_IN_SCOPE", "By-Others" };
    // Auto values written on the excluded panel; any refresh would replace them with measured ones.
    private static readonly Dictionary<string, string> Sentinels = new Dictionary<string, string>
    {
        ["CW_2.00_UNIT_DIMENSION"] = "1.00000x1.00000", ["CW_2.01_UNIT_WIDTH"] = "1.00000", ["CW_2.02_UNIT_HEIGHT"] = "1.00000",
        ["CW_2.07_UNIT_AREA"] = "0.007 sqf", ["CW_6.02_UNIT_WEIGHT"] = "0.574", ["UD_PLANE"] = "{0, 0, 0};{1,0,0};{0,0,1}"
    };

    private static List<Step> ExcludedSteps()
    {
        var steps = new List<Step>
        {
            new Step("3.0 undo an earlier run", ctx =>
            {
                RhinoDoc doc = ctx.Doc;
                RenameLayer(doc, ExcludedLayer(doc), ExcludedNames[0]);
                ctx.Bag["restored"] = RestoreMovedPanels(doc);
                foreach (var item in doc.Objects.GetObjectList(Everything(Rhino.DocObjects.ObjectType.Brep))
                    .Where(item => item.Attributes.GetUserString(NewPanelKey) != null).ToList())
                {
                    doc.Objects.Delete(item.Id, true);
                }
                return true;
            }),
            new Step("3.0 setup", ctx =>
            {
                RhinoDoc doc = ctx.Doc;
                // Panels moved back into scope get their auto values refreshed on idle; wait for that.
                var restored = (List<Guid>)ctx.Bag["restored"];
                if (restored.Any(id => doc.Objects.FindId(id).Attributes.GetUserString("UD_PLANE") == Sentinels["UD_PLANE"]) &&
                    (DateTime.Now - ctx.StepStarted).TotalSeconds < 15) return false;
                int excluded = ExcludedLayer(doc);
                List<Rhino.DocObjects.RhinoObject> panels = PanelsOn(doc, TestPanelLayer);
                Rhino.DocObjects.RhinoObject d = panels[3], e = panels[4];
                ctx.Bag["panelD"] = d.Id;
                ctx.Bag["panelE"] = e.Id;
                DeleteManaged(doc, d);
                WriteLayout(doc, d.Id, 0.5, 0.5, FourCells);
                var attributes = doc.Objects.FindId(d.Id).Attributes.Duplicate();
                attributes.LayerIndex = excluded;
                attributes.SetUserString(OriginLayerKey, TestPanelLayer);
                doc.Objects.ModifyAttributes(d.Id, attributes, true);
                // PCpid numbers a new copy of panel E that has no PID and no generated dependencies.
                var newAttributes = new Rhino.DocObjects.ObjectAttributes { LayerIndex = e.Attributes.LayerIndex };
                newAttributes.SetUserString(NewPanelKey, "1");
                // Raised well above the building, so it occupies a position of its own.
                Rhino.Geometry.Brep copyGeometry = ((Rhino.Geometry.Brep)e.Geometry).DuplicateBrep();
                copyGeometry.Transform(Rhino.Geometry.Transform.Translation(0, 0, 3000));
                Guid fresh = doc.Objects.AddBrep(copyGeometry, newAttributes);
                ctx.Bag["panelNew"] = fresh;
                ctx.Note("panel D (excluded)", new { id = d.Id, pid = Pid(d), layer = doc.Layers[excluded].FullPath });
                ctx.Note("panel E (control)", new { id = e.Id, pid = Pid(e) });
                ctx.Note("3.0 new panel for PCpid", fresh);
                return true;
            }, 200),
            new Step("3.0 stale auto values", ctx =>
            {
                RhinoDoc doc = ctx.Doc;
                Guid d = (Guid)ctx.Bag["panelD"], e = (Guid)ctx.Bag["panelE"];
                var attributes = doc.Objects.FindId(d).Attributes.Duplicate();
                ctx.Bag["realAuto"] = Sentinels.Keys.ToDictionary(key => key, key => attributes.GetUserString(key) ?? string.Empty);
                foreach (var pair in Sentinels) attributes.SetUserString(pair.Key, pair.Value);
                doc.Objects.ModifyAttributes(d, attributes, true);
                var control = doc.Objects.FindId(e).Attributes.Duplicate();
                ctx.Note("panel E width before reopen", control.GetUserString("CW_2.01_UNIT_WIDTH"));
                control.SetUserString("CW_2.01_UNIT_WIDTH", "1.00000");
                doc.Objects.ModifyAttributes(e, control, true);
                return true;
            }),
            new Step("3.1 save and reopen", ctx =>
            {
                RhinoDoc doc = ctx.Doc;
                string path = doc.Path;
                uint before = doc.RuntimeSerialNumber;
                bool saved = doc.Save();
                ctx.Bag["historyAtOpen"] = (RhinoApp.CommandHistoryWindowText ?? string.Empty).Length;
                string history = ctx.Cmd("_-Open \"" + path + "\"");
                ctx.Serial = RhinoDoc.ActiveDoc.RuntimeSerialNumber;
                ctx.Check("3.1 model saved and reopened", saved && ctx.Serial != before &&
                    string.Equals(RhinoDoc.ActiveDoc.Path, path, StringComparison.OrdinalIgnoreCase), new { saved, history });
                return true;
            }),
            new Step("3.1 auto values after reopen", ctx =>
            {
                RhinoDoc doc = ctx.Doc;
                Guid d = (Guid)ctx.Bag["panelD"], e = (Guid)ctx.Bag["panelE"];
                string width = doc.Objects.FindId(e)?.Attributes.GetUserString("CW_2.01_UNIT_WIDTH");
                if (width == "1.00000" && (DateTime.Now - ctx.StepStarted).TotalSeconds < 20) return false;
                string all = RhinoApp.CommandHistoryWindowText ?? string.Empty;
                int start = Math.Min(all.Length, (int)ctx.Bag["historyAtOpen"]);
                ctx.Check("3.1 control panel E was refreshed on open", width != "1.00000", new { width, history = all.Substring(start) });
                CheckSentinels(ctx, "3.1 Not in Scope panel keeps its auto values after reopening", d);
                return true;
            }, 200),
            PidStep("3.2 PCpid does not number the Not in Scope panel"),
            BakeExcludedStep("3.3 PCbake Curves fails on the Not in Scope panel"),
        };
        foreach (string name in ExcludedNames.Skip(1))
        {
            steps.Add(new Step("3.4 rename to " + name, ctx =>
            {
                RenameLayer(ctx.Doc, ExcludedLayer(ctx.Doc), name);
                return true;
            }));
            steps.Add(new Step("3.4 " + name + " auto values", ctx =>
            {
                // Renaming a layer queues the same full scan as opening the model; give it time to run.
                if ((DateTime.Now - ctx.StepStarted).TotalSeconds < 2) return false;
                CheckSentinels(ctx, "3.4 " + name + ": auto values are not refreshed", (Guid)ctx.Bag["panelD"]);
                return true;
            }, 200));
            steps.Add(PidStep("3.4 " + name + ": PCpid does not number it"));
            steps.Add(BakeExcludedStep("3.4 " + name + ": PCbake Curves fails"));
        }
        steps.Add(new Step("3.5 restore layer name", ctx =>
        {
            RenameLayer(ctx.Doc, ExcludedLayer(ctx.Doc), ExcludedNames[0]);
            ctx.Note("3.5 naming convention", "Not testable here: the sync writes only for a main model " +
                "(<code> - Wireframe.3dm) with <code> - Naming Convention.xlsx beside it; no XYZ sheet exists.");
            return true;
        }));
        return steps;
    }

    private static Step PidStep(string id) => new Step(id, ctx =>
    {
        RhinoDoc doc = ctx.Doc;
        Guid d = (Guid)ctx.Bag["panelD"], fresh = (Guid)ctx.Bag["panelNew"];
        List<Rhino.DocObjects.RhinoObject> inScope = InScopePanels(doc);
        Rhino.DocObjects.RhinoObject north = inScope.First(item => !item.IsLocked && !item.IsHidden &&
            string.Equals(item.Attributes.GetUserString("CW_1.03_ELEVATION"), "NORTH", StringComparison.OrdinalIgnoreCase));
        Rhino.DocObjects.RhinoObject floor = inScope.First(item => !item.IsLocked && !item.IsHidden &&
            (item.Attributes.GetUserString("CW_1.04_LEVEL") ?? string.Empty).Trim() == "01");
        string pidBefore = Pid(doc.Objects.FindId(d));
        string[] renamed = SuffixDuplicatePids(doc, inScope);
        SelectOnly(doc, d, fresh);
        string history = ctx.Cmd("_PCpid _Enter '_SelID " + north.Id + " '_SelID " + floor.Id);
        string pidAfter = Pid(doc.Objects.FindId(d));
        ctx.Check(id, history.Contains("PCpid uses all " + inScope.Count + " panels") &&
            history.Contains("assigned 1 selected panel IDs") && pidAfter == pidBefore,
            new { inScopePanels = inScope.Count, pidBefore, pidAfter, newPanelPid = Pid(doc.Objects.FindId(fresh)), duplicatePidsSuffixed = renamed, history });
        doc.Objects.UnselectAll();
        return true;
    });

    // PCpid refuses to number while the panel branch holds duplicate PIDs, which this model has; the
    // extra panels of each duplicate group get a -LIVEDUPn suffix (test data only).
    private static string[] SuffixDuplicatePids(RhinoDoc doc, List<Rhino.DocObjects.RhinoObject> inScope)
    {
        var renamed = new List<string>();
        foreach (var group in inScope.GroupBy(Pid, StringComparer.OrdinalIgnoreCase).Where(group => group.Key.Length > 0 && group.Count() > 1))
        {
            int index = 0;
            foreach (Rhino.DocObjects.RhinoObject duplicate in group.Skip(1))
            {
                var copy = duplicate.Attributes.Duplicate();
                string pid = group.Key + "-LIVEDUP" + ++index;
                copy.SetUserString(PidKey, pid);
                doc.Objects.ModifyAttributes(duplicate.Id, copy, true);
                renamed.Add(group.Key + " -> " + pid);
            }
        }
        return renamed.ToArray();
    }

    private static Step BakeExcludedStep(string id) => new Step(id, ctx =>
    {
        RhinoDoc doc = ctx.Doc;
        Guid d = (Guid)ctx.Bag["panelD"];
        string curves = CurveFingerprint(doc);
        SelectOnly(doc, d);
        // With the stale test values (a fake UD_PLANE) the panel extent check fails first; record that once.
        if (!ctx.Notes.ContainsKey("3.3 stale auto values"))
        {
            ctx.Note("3.3 stale auto values", ctx.Cmd("_-PCbake _Scope=_Curves _Enter"));
        }
        WriteValues(doc, d, (Dictionary<string, string>)ctx.Bag["realAuto"]);
        SelectOnly(doc, d);
        string history = ctx.Cmd("_-PCbake _Scope=_Curves _Enter");
        WriteValues(doc, d, Sentinels);
        ctx.Check(id, history.Contains("PANEL_CLADDING_PANEL_LAYER_EXCLUDED") && ManagedCurves(doc, d).Count == 0 &&
            CurveFingerprint(doc) == curves, history);
        doc.Objects.UnselectAll();
        return true;
    });

    private static void WriteValues(RhinoDoc doc, Guid id, Dictionary<string, string> values)
    {
        var attributes = doc.Objects.FindId(id).Attributes.Duplicate();
        foreach (var pair in values) attributes.SetUserString(pair.Key, pair.Value);
        doc.Objects.ModifyAttributes(id, attributes, true);
    }

    private static void CheckSentinels(Ctx ctx, string id, Guid panelId)
    {
        Rhino.DocObjects.RhinoObject panel = ctx.Doc.Objects.FindId(panelId);
        var values = Sentinels.Keys.ToDictionary(key => key, key => panel.Attributes.GetUserString(key));
        ctx.Check(id, Sentinels.All(pair => values[pair.Key] == pair.Value), new { layer = LayerPath(ctx.Doc, panel), values });
    }

    private static bool IsExcludedPath(string path) =>
        path.Split(new[] { "::" }, StringSplitOptions.None).Any(segment =>
        {
            string word = new string(segment.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
            return word.Contains("notinscope") || word.Contains("byothers");
        });

    // Panels PCpid numbers: surfaces and Breps on the panel branch outside excluded layers.
    private static List<Rhino.DocObjects.RhinoObject> InScopePanels(RhinoDoc doc) =>
        doc.Objects.GetObjectList(Everything(Rhino.DocObjects.ObjectType.Brep | Rhino.DocObjects.ObjectType.Surface))
            .Where(item => Under(LayerPath(doc, item), PanelBranch) && !IsExcludedPath(LayerPath(doc, item)))
            .OrderBy(Pid, StringComparer.Ordinal).ToList();

    private static int ExcludedLayer(RhinoDoc doc)
    {
        Rhino.DocObjects.Layer parent = doc.Layers[doc.Layers.FindByFullPath(PanelBranch, -1)];
        Rhino.DocObjects.Layer existing = doc.Layers.FirstOrDefault(layer => !layer.IsDeleted &&
            layer.ParentLayerId == parent.Id && ExcludedNames.Contains(layer.Name));
        return existing?.Index ?? doc.Layers.Add(new Rhino.DocObjects.Layer { Name = ExcludedNames[0], ParentLayerId = parent.Id });
    }

    private static void RenameLayer(RhinoDoc doc, int index, string name)
    {
        Rhino.DocObjects.Layer layer = doc.Layers[index];
        if (layer.Name == name) return;
        layer.Name = name;
        doc.Layers.Modify(layer, index, true);
    }

    // Puts panels an earlier run moved to the excluded layer back where they came from.
    private static List<Guid> RestoreMovedPanels(RhinoDoc doc)
    {
        var restored = new List<Guid>();
        foreach (var item in doc.Objects.GetObjectList(Everything(Rhino.DocObjects.ObjectType.Brep))
            .Where(item => !string.IsNullOrEmpty(item.Attributes.GetUserString(OriginLayerKey))).ToList())
        {
            var attributes = item.Attributes.Duplicate();
            attributes.LayerIndex = doc.Layers.FindByFullPath(attributes.GetUserString(OriginLayerKey), -1);
            attributes.DeleteUserString(OriginLayerKey);
            doc.Objects.ModifyAttributes(item.Id, attributes, true);
            restored.Add(item.Id);
        }
        return restored;
    }

    // ---------------------------------------------------------------- 4. PCreport

    private static object ReportWindow() => R.StaticField("PanelCladdingEditor.UI.PanelCladdingReportCommand", "_window");

    private static List<Step> ReportSteps() => new List<Step>
    {
        new Step("4.1 window opens read-only", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            doc.Objects.UnselectAll();
            // A known undoable step first: if PCreport adds nothing to the Undo list, Undo afterwards undoes the point.
            ctx.Cmd("_Point 0,0,-10000");
            doc.Modified = false;
            uint undo = doc.NextUndoRecordSerialNumber;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            string history = ctx.Cmd("_PCreport");
            watch.Stop();
            bool modified = doc.Modified;
            uint undoAfter = doc.NextUndoRecordSerialNumber;
            string undoRun = ctx.Cmd("_Undo");
            uint baseline = doc.NextUndoRecordSerialNumber;
            ctx.Cmd("_SelNone");
            uint baselineAfter = doc.NextUndoRecordSerialNumber;
            object window = ReportWindow();
            var counts = new
            {
                errors = R.Text(window, "ErrorCountText"), warnings = R.Text(window, "WarningCountText"),
                info = R.Text(window, "InfoCountText"), document = R.Text(window, "DocumentText")
            };
            ctx.Check("4.1 window opens with error, warning and info counts", window != null && (bool)R.Get(window, "IsVisible") &&
                counts.errors.Length > 0 && counts.warnings.Length > 0 && counts.info.Length > 0 && history.Contains("PCreport: "),
                new { counts, history, seconds = watch.Elapsed.TotalSeconds });
            ctx.Check("4.2 model not modified, nothing added to Undo", !modified && undoRun.Contains("Undoing Point"),
                new
                {
                    modified, undoRun,
                    note = "Rhino reserves an undo serial for every command, read-only ones included",
                    serialAcrossPCreport = new[] { undo, undoAfter }, serialAcrossSelNone = new[] { baseline, baselineAfter }
                });
            R.Render(window, ctx.EvidenceFile("report-window.png"));
            return true;
        }),
        new Step("4.3 rule results", ctx =>
        {
            object report = R.Get(ReportWindow(), "Report");
            var issues = ((System.Collections.IEnumerable)R.Get(report, "Issues")).Cast<object>().ToList();
            var rules = issues.GroupBy(issue => R.Get(issue, "Rule")).Select(group => new
            {
                id = (string)R.Get(group.Key, "Id"), title = (string)R.Get(group.Key, "Title"),
                severity = R.Get(group.Key, "Severity").ToString(), category = R.Get(group.Key, "Category").ToString(),
                count = group.Count(), objectIssues = group.Count(issue => (Guid)R.Get(issue, "ObjectId") != Guid.Empty),
                byLayer = group.GroupBy(issue => (string)R.Get(issue, "LayerPath")).OrderByDescending(layer => layer.Count()).Take(12)
                    .ToDictionary(layer => layer.Key, layer => layer.Count()),
                samples = group.Take(5).Select(issue => new
                {
                    objectId = (Guid)R.Get(issue, "ObjectId"), name = (string)R.Get(issue, "ObjectName"),
                    layer = (string)R.Get(issue, "LayerPath"), detail = (string)R.Get(issue, "Detail")
                }).ToArray()
            }).OrderBy(rule => rule.id).ToArray();
            System.IO.File.WriteAllText(ctx.EvidenceFile("report-rules.json"), JsonSerializer.Serialize(new
            {
                panels = R.Get(report, "PanelCount"), surfaces = R.Get(report, "SurfaceCount"), curves = R.Get(report, "CurveCount"), rules
            }, new JsonSerializerOptions { WriteIndented = true }));
            ctx.Note("4.3 rules", rules.Select(rule => rule.id + " " + rule.severity + " x" + rule.count + ": " + rule.title).ToArray());
            return true;
        }),
        new Step("4.4 clicking rows selects and zooms", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            object window = ReportWindow();
            // Two visible, unlocked object issues of one rule.
            var candidates = IssueRows(window).Where(pair =>
            {
                Rhino.DocObjects.RhinoObject item = doc.Objects.FindId(pair.Id);
                return item != null && !item.IsLocked && !item.IsHidden && item.Visible;
            }).ToList();
            var rule = candidates.GroupBy(pair => pair.Rule).First(group => group.Count() >= 3);
            var rows = rule.Take(3).ToList();
            R.Set(R.Find(window, "RuleList"), "SelectedItem", rule.Key);
            object issueList = R.Find(window, "IssueList");
            doc.Objects.UnselectAll();
            Rhino.Geometry.Point3d cameraBefore = doc.Views.ActiveView.ActiveViewport.CameraTarget;
            R.Set(issueList, "SelectedItem", rows[0].Row);
            Guid[] selected = doc.Objects.GetSelectedObjects(false, false).Select(item => item.Id).ToArray();
            Rhino.Geometry.Point3d target = doc.Views.ActiveView.ActiveViewport.CameraTarget;
            Rhino.Geometry.BoundingBox box = doc.Objects.FindId(rows[0].Id).Geometry.GetBoundingBox(true);
            ctx.Check("4.4 clicking a row selects its object", selected.Length == 1 && selected[0] == rows[0].Id,
                new { rule = R.Get(rule.Key, "Title"), row = rows[0].Id, selected });
            ctx.Check("4.4 and zooms to it", target.DistanceTo(box.Center) < Math.Max(1, box.Diagonal.Length) && target.DistanceTo(cameraBefore) > 1e-6,
                new { cameraBefore = cameraBefore.ToString(), cameraTarget = target.ToString(), objectCenter = box.Center.ToString() });
            CaptureView(ctx, "report-click-zoom.png");
            var list = (System.Collections.IList)R.Get(issueList, "SelectedItems");
            list.Add(rows[1].Row);
            list.Add(rows[2].Row);
            var many = doc.Objects.GetSelectedObjects(false, false).Select(item => item.Id).OrderBy(id => id).ToArray();
            ctx.Check("4.5 selecting several rows selects all their objects",
                many.SequenceEqual(rows.Select(row => row.Id).OrderBy(id => id)), new { expected = rows.Select(row => row.Id), selected = many });
            ctx.Bag["reportRows"] = rows.Select(row => row.Id).ToArray();
            return true;
        }),
        new Step("4.6 locked and hidden objects are zoomed to, not selected", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            object window = ReportWindow();
            object issueList = R.Find(window, "IssueList");
            var rows = IssueRows(window).Where(pair => (pair.Rule == R.Get(R.Find(window, "RuleList"), "SelectedItem")) &&
                doc.Objects.FindId(pair.Id) != null).Skip(3).Take(2).ToList();
            Guid locked = rows[0].Id, hidden = rows[1].Id;
            doc.Objects.Lock(locked, true);
            doc.Objects.Hide(hidden, true);
            try
            {
                foreach (var pair in new[] { (rows[0], "locked"), (rows[1], "hidden") })
                {
                    doc.Objects.UnselectAll();
                    R.Call(issueList, "UnselectAll");
                    Rhino.Geometry.Point3d before = doc.Views.ActiveView.ActiveViewport.CameraTarget;
                    R.Set(issueList, "SelectedItem", pair.Item1.Row);
                    Rhino.Geometry.BoundingBox box = doc.Objects.FindId(pair.Item1.Id).Geometry.GetBoundingBox(true);
                    Rhino.Geometry.Point3d after = doc.Views.ActiveView.ActiveViewport.CameraTarget;
                    int selected = doc.Objects.GetSelectedObjects(false, false).Count();
                    ctx.Check("4.6 " + pair.Item2 + " object: zoomed, not selected",
                        selected == 0 && after.DistanceTo(box.Center) < Math.Max(1, box.Diagonal.Length) && after.DistanceTo(before) > 1e-6,
                        new { id = pair.Item1.Id, selected, target = after.ToString(), center = box.Center.ToString() });
                }
            }
            finally
            {
                doc.Objects.Unlock(locked, true);
                doc.Objects.Show(hidden, true);
            }
            return true;
        }),
        new Step("4.7 layer-level row selects nothing", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            object window = ReportWindow();
            var layerRow = IssueRowsAll(window).FirstOrDefault(pair => pair.Id == Guid.Empty);
            if (layerRow.Row == null)
            {
                ctx.Check("4.7 a layer-level row exists in this model", false, "No layer-level issue to click.");
                return true;
            }
            R.Set(R.Find(window, "RuleList"), "SelectedItem", layerRow.Rule);
            object issueList = R.Find(window, "IssueList");
            doc.Objects.UnselectAll();
            R.Set(issueList, "SelectedItem", layerRow.Row);
            int fromClean = doc.Objects.GetSelectedObjects(false, false).Count();
            ctx.Check("4.7 clicking a layer-level row selects nothing", fromClean == 0,
                new { rule = R.Get(layerRow.Rule, "Title"), layer = R.Get(layerRow.Row, "Layer"), selected = fromClean });
            return true;
        }),
        new Step("4.8 Rescan after a fix", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            object window = ReportWindow();
            // Fix one "Name does not match CID" issue by giving the panel the expected name.
            IssuePick pick = IssueRows(window).First(pair => (string)R.Get(pair.Rule, "Title") == "Name does not match CID" &&
                doc.Objects.FindId(pair.Id) is Rhino.DocObjects.RhinoObject item && !item.IsLocked);
            string detail = (string)R.Get(pick.Row, "Detail");
            var match = System.Text.RegularExpressions.Regex.Match(detail, "expected '([^']+)'");
            int before = RuleCount(window, "Name does not match CID");
            var attributes = doc.Objects.FindId(pick.Id).Attributes.Duplicate();
            attributes.Name = match.Groups[1].Value;
            doc.Objects.ModifyAttributes(pick.Id, attributes, true);
            R.Click(R.Find(window, "RescanButton"));
            int after = RuleCount(window, "Name does not match CID");
            bool gone = !IssueRows(window).Any(pair => pair.Id == pick.Id && (string)R.Get(pair.Rule, "Title") == "Name does not match CID");
            ctx.Check("4.8 Rescan shows the fixed issue gone", match.Success && after == before - 1 && gone,
                new { panel = pick.Id, detail, before, after, counts = R.Text(window, "WarningCountText") });
            return true;
        }),
        new Step("4.9 Rhino stays usable while the window is open", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            object window = ReportWindow();
            string history = ctx.Cmd("_SelNone");
            Type dispatcher = R.T("System.Windows.Interop.ComponentDispatcher");
            bool modal = (bool)dispatcher.GetProperty("IsThreadModal").GetValue(null);
            ctx.Check("4.9 a command runs while the window is open; the window is modeless",
                (bool)R.Get(window, "IsVisible") && !modal && history.Contains("Command: _SelNone") && !history.Contains("Unknown"),
                new { history, isThreadModal = modal });
            return true;
        }),
    };

    private struct IssuePick
    {
        public object Rule;
        public object Row;
        public Guid Id;
    }

    private static IEnumerable<IssuePick> IssueRowsAll(object window)
    {
        object view = R.Get(R.Find(window, "RuleList"), "ItemsSource");
        foreach (object rule in ((System.Collections.IEnumerable)R.Get(view, "SourceCollection")).Cast<object>())
        {
            foreach (object row in ((System.Collections.IEnumerable)R.Get(rule, "Issues")).Cast<object>())
            {
                yield return new IssuePick { Rule = rule, Row = row, Id = (Guid)R.Get(row, "ObjectId") };
            }
        }
    }

    private static IEnumerable<IssuePick> IssueRows(object window) => IssueRowsAll(window).Where(pair => pair.Id != Guid.Empty);

    private static int RuleCount(object window, string title) =>
        IssueRowsAll(window).Count(pair => (string)R.Get(pair.Rule, "Title") == title);

    private static void CaptureView(Ctx ctx, string name)
    {
        Rhino.Display.RhinoView view = ctx.Doc.Views.ActiveView;
        System.Reflection.MethodInfo capture = typeof(Rhino.Display.RhinoView).GetMethods()
            .First(method => method.Name == "CaptureToBitmap" && method.GetParameters().Length == 0);
        object bitmap = capture.Invoke(view, null);
        bitmap.GetType().GetMethod("Save", new[] { typeof(string) }).Invoke(bitmap, new object[] { ctx.EvidenceFile(name) });
        ((IDisposable)bitmap).Dispose();
    }

    // Export runs with tests/Live/Watch-Dialogs.ps1 answering the Save dialogs and
    // tests/Live/Hold-Workbook.ps1 holding the exported workbook open in Excel.
    private static List<Step> ExportSteps() => new List<Step>
    {
        new Step("4.10 export with the default name", ctx =>
        {
            object window = ReportWindow() ?? throw new InvalidOperationException("Open PCreport first (suite report).");
            string model = ctx.Doc.Path;
            string expected = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(model),
                System.IO.Path.GetFileNameWithoutExtension(model) + " - PC Report.xlsx");
            if (System.IO.File.Exists(expected)) System.IO.File.Delete(expected);
            ctx.Bag["workbook"] = expected;
            R.Click(R.Find(window, "ExportButton"));
            string status = R.Text(window, "StatusText");
            ctx.Check("4.10 default name is '<model> - PC Report.xlsx' next to the model", System.IO.File.Exists(expected) &&
                status == "Exported " + System.IO.Path.GetFileName(expected), new { expected, status });
            System.IO.File.WriteAllText(ctx.EvidenceFile("export-path.txt"), expected);
            return true;
        }),
        new Step("4.10 wait for Excel", ctx =>
            System.IO.File.Exists(ctx.EvidenceFile("excel-holding.txt")) || (DateTime.Now - ctx.StepStarted).TotalSeconds > 90, 2000),
        new Step("4.10 export while the workbook is open in Excel", ctx =>
        {
            object window = ReportWindow();
            string holding = System.IO.File.Exists(ctx.EvidenceFile("excel-holding.txt")) ? System.IO.File.ReadAllText(ctx.EvidenceFile("excel-holding.txt")) : string.Empty;
            R.Click(R.Find(window, "ExportButton"));
            string status = R.Text(window, "StatusText");
            string history = ctx.Cmd("_SelNone");
            ctx.Check("4.10 exporting over the open workbook shows an error instead of crashing", holding.Length > 0 &&
                status.Length > 0 && !status.StartsWith("Exported") && (bool)R.Get(window, "IsVisible") && history.Contains("_SelNone"),
                new { holding, status, leftover = System.IO.Directory.GetFiles(System.IO.Path.GetDirectoryName(ctx.Doc.Path), "*.writing") });
            R.Render(window, ctx.EvidenceFile("report-export-error.png"));
            System.IO.File.WriteAllText(ctx.EvidenceFile("rhino-done.txt"), DateTime.Now.ToString("s"));
            return true;
        }),
    };

    // Runs in a second Rhino started on the ABC copy, so the XYZ session is not replaced. Parameters:
    // "mainModel" = a copy of a project model saved under its main-model name (XYZ - Wireframe.3dm)
    // with a "<code> - Naming Convention.xlsx" beside it (tests/Live/New-NamingSheet.ps1).
    private static List<Step> OtherDocumentSteps() => new List<Step>
    {
        new Step("4.14 speed on a large model", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            int objects = doc.Objects.Count;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            string history = ctx.Cmd("_PCreport");
            watch.Stop();
            object window = ReportWindow();
            object report = R.Get(window, "Report");
            var issues = ((System.Collections.IEnumerable)R.Get(report, "Issues")).Cast<object>().ToList();
            var glsInfo = issues.Where(issue => R.Get(R.Get(issue, "Rule"), "Severity").ToString() == "Info" &&
                ((string)R.Get(issue, "LayerPath")).IndexOf("Surfaces - GLS", StringComparison.OrdinalIgnoreCase) >= 0)
                .GroupBy(issue => (string)R.Get(R.Get(issue, "Rule"), "Id") + " " + (string)R.Get(R.Get(issue, "Rule"), "Title"))
                .ToDictionary(group => group.Key, group => group.Count());
            var rules = issues.GroupBy(issue => (string)R.Get(R.Get(issue, "Rule"), "Id") + " " + R.Get(R.Get(issue, "Rule"), "Severity") + ": " +
                (string)R.Get(R.Get(issue, "Rule"), "Title")).OrderBy(group => group.Key).ToDictionary(group => group.Key, group => group.Count());
            ctx.Note("4.14 ABC rules", rules);
            ctx.Note("4.3 ABC Surfaces - GLS info", glsInfo);
            ctx.Check("4.14 PCreport on ABC finishes within 30 s", watch.Elapsed.TotalSeconds < 30 && history.Contains("PCreport: "),
                new { seconds = Math.Round(watch.Elapsed.TotalSeconds, 2), objects, history });
            R.Render(window, ctx.EvidenceFile("report-window-ABC.png"));
            ctx.Bag["firstWindow"] = window;
            return true;
        }),
        new Step("4.12 PCreport in another model replaces the window's contents", ctx =>
        {
            string mainModel = Text(ctx.Parameters, "mainModel");
            ctx.Doc.Modified = false;
            string open = ctx.Cmd("_-Open \"" + mainModel + "\"");
            ctx.Serial = RhinoDoc.ActiveDoc.RuntimeSerialNumber;
            string history = ctx.Cmd("_PCreport");
            object first = ctx.Bag["firstWindow"], window = ReportWindow();
            string document = R.Text(window, "DocumentText");
            string path = (string)R.Get(R.Get(window, "Report"), "DocumentPath");
            ctx.Check("4.12 the window now shows the other model's results", window != null && (bool)R.Get(window, "IsVisible") &&
                document == System.IO.Path.GetFileNameWithoutExtension(mainModel) &&
                string.Equals(path, mainModel, StringComparison.OrdinalIgnoreCase) && !(bool)R.Get(first, "IsVisible"),
                new { document, path, sameWindowObject = ReferenceEquals(first, window), firstStillVisible = R.Get(first, "IsVisible"), open, history });
            R.Render(window, ctx.EvidenceFile("report-window-other-model.png"));
            return true;
        }),
        new Step("3.6 saving the main model leaves excluded panels out of the naming sheet", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            string sheet = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(doc.Path), "XYZ - Naming Convention.xlsx");
            List<Rhino.DocObjects.RhinoObject> panels = PanelsOn(doc, TestPanelLayer);
            int excluded = ExcludedLayer(doc);
            var outside = panels[0].Attributes.Duplicate();
            outside.LayerIndex = excluded;
            outside.SetUserString("CW_1.07_WALL_TYPE", "NIS-LIVE");
            doc.Objects.ModifyAttributes(panels[0].Id, outside, true);
            var inside = panels[1].Attributes.Duplicate();
            inside.SetUserString("CW_1.07_WALL_TYPE", "INS-LIVE");
            doc.Objects.ModifyAttributes(panels[1].Id, inside, true);
            string before = RhinoApp.CommandHistoryWindowText ?? string.Empty;
            bool saved = doc.Save();
            string after = RhinoApp.CommandHistoryWindowText ?? string.Empty;
            object reader = Activator.CreateInstance(R.T("PanelCladdingEditor.Infrastructure.PanelCladding.OpenXmlPanelNamingConventionWorkbook"));
            object read = R.Call(reader, "Read", sheet);
            object content = R.Get(read, "Data");
            var walls = content == null ? new Dictionary<string, string>() :
                ((System.Collections.IEnumerable)R.Get(content, "WallTypes")).Cast<object>()
                    .ToDictionary(pair => (string)R.Get(pair, "Key"), pair => (string)R.Get(pair, "Value"));
            ctx.Check("3.6 the sheet lists the in-scope wall type and not the excluded one", saved && content != null &&
                walls.ContainsValue("INS-LIVE") && !walls.ContainsValue("NIS-LIVE"),
                new { sheet, saved, readMessage = R.Get(read, "Message"), wallTypes = walls,
                    history = after.StartsWith(before, StringComparison.Ordinal) ? after.Substring(before.Length) : after.Substring(Math.Max(0, after.Length - 1500)) });
            return true;
        }),
        new Step("4.13 PCreport in an unsaved new document asks to save first", ctx =>
        {
            ctx.Doc.Modified = false;
            string created = ctx.Cmd("_-New _None");
            ctx.Serial = RhinoDoc.ActiveDoc.RuntimeSerialNumber;
            string history = ctx.Cmd("_PCreport");
            ctx.Check("4.13 an unsaved document gets 'requires a saved active document'", string.IsNullOrEmpty(RhinoDoc.ActiveDoc.Path) &&
                history.Contains("PCreport requires a saved active document."), new { created, history });
            return true;
        }),
    };

    // ---------------------------------------------------------------- 5. PCeditor

    private const string EditorPanelLayer = PanelBranch + "::PNL-EXT-3A.4";
    private const string AssignmentsPayload = "{\"v\":1,\"f\":{\"FRM-0\":[\"1D-T000\"]},\"s\":[]}";

    private static object EditorWindow() => R.StaticField("PanelCladdingEditor.UI.PanelCladdingEditorCommand", "_window");

    // Runs with tests/Live/Watch-Dialogs.ps1 answering the discard prompts "No" then "Yes".
    private static List<Step> EditorSteps() => new List<Step>
    {
        new Step("5.0 setup and open PCeditor", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            Rhino.DocObjects.RhinoObject panel = PanelsOn(doc, EditorPanelLayer)[0];
            ctx.Bag["panelF"] = panel.Id;
            WriteLayout(doc, panel.Id, 0.5, 0.5, FourCells);
            var attributes = doc.Objects.FindId(panel.Id).Attributes.Duplicate();
            attributes.SetUserString(FrameAssignmentsKey, AssignmentsPayload);
            doc.Objects.ModifyAttributes(panel.Id, attributes, true);
            SelectOnly(doc, panel.Id);
            string history = ctx.Cmd("_PCeditor");
            object window = EditorWindow();
            ctx.Check("5.0 PCeditor opens on the panel", window != null && (bool)R.Get(window, "IsVisible"),
                new { panel = panel.Id, pid = Pid(panel), history, status = R.Text(window, "FooterStatusText"), badge = R.Text(window, "DirtyBadgeText") });
            doc.Objects.UnselectAll();
            return true;
        }),
        new Step("5.4 Cladding view", ctx =>
        {
            object window = EditorWindow();
            var state = Visibilities(window, "MaterialLegendSection", "CladdingAssignmentSection", "ClearCellsButton",
                "ExtrusionActions", "FrameConfigurationSection", "ExtrusionLegendSection", "ExtrusionAssignmentSection");
            ctx.Check("5.4 Cladding view shows the material legend and cell assignment", state["MaterialLegendSection"] == "Visible" &&
                state["CladdingAssignmentSection"] == "Visible" && state["ClearCellsButton"] == "Visible" && state["ExtrusionActions"] != "Visible", state);
            R.Render(window, ctx.EvidenceFile("editor-cladding-view.png"));
            return true;
        }),
        new Step("5.1 Extrusion view", ctx =>
        {
            object window = EditorWindow();
            R.Click(R.Find(window, "ExtrusionViewButton"));
            var state = Visibilities(window, "ExtrusionActions", "MergeExtrusionsButton", "ExplodeExtrusionsButton", "HideExtrusionsButton",
                "AddHorizontalMullionButton", "AddVerticalMullionButton", "DeleteExtrusionsButton", "DividerOffsetList",
                "ExtrusionLegendSection", "ExtrusionAssignmentSection", "FrameConfigurationNote", "FrameConfigurationSection", "MaterialLegendSection");
            string[] actions = { "ExtrusionActions", "MergeExtrusionsButton", "ExplodeExtrusionsButton", "HideExtrusionsButton",
                "AddHorizontalMullionButton", "AddVerticalMullionButton", "DeleteExtrusionsButton", "DividerOffsetList", "FrameConfigurationSection" };
            ctx.Check("5.1 Extrusion view offers merge, explode, hide, add, delete and dimensions", actions.All(name => state[name] == "Visible"), state);
            // Select a frame segment and an intermediate one: the floating assignment panel must stay hidden.
            var segments = Segments(window);
            var selection = (System.Collections.Generic.ISet<string>)R.Get(window, "_selectedExtrusions");
            selection.Clear();
            selection.Add(segments.First(segment => segment.IsFrame).Id);
            selection.Add(segments.First(segment => !segment.IsFrame).Id);
            R.Call(window, "UpdateExtrusionSelectionUi");
            var map = (System.Collections.IDictionary)R.Call(window, "VisibleAssignmentMap");
            var after = Visibilities(window, "ExtrusionLegendSection", "ExtrusionAssignmentSection", "FrameConfigurationNote");
            ctx.Check("5.2 no profile catalogue, floating assignment panel or profile codes on curve labels",
                after.Values.All(value => value != "Visible") && map.Count == 0,
                new { after, labelCodes = map.Count, selected = selection.ToArray(), tooltip = R.Get(R.Find(window, "FrameConfigurationText"), "ToolTip") });
            R.Render(window, ctx.EvidenceFile("editor-extrusion-view.png"));
            selection.Clear();
            R.Call(window, "UpdateExtrusionSelectionUi");
            return true;
        }),
        new Step("5.3 layout edit and Save Extrusions keep UD_FRAME_ASSIGNMENTS", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            object window = EditorWindow();
            Guid panel = (Guid)ctx.Bag["panelF"];
            string before = doc.Objects.FindId(panel).Attributes.GetUserString(FrameAssignmentsKey);
            var segment = Segments(window).First(item => !item.IsFrame && item.Axis == "Vertical");
            var selection = (System.Collections.Generic.ISet<string>)R.Get(window, "_selectedExtrusions");
            selection.Clear();
            selection.Add(segment.Id);
            R.Call(window, "UpdateExtrusionSelectionUi");
            R.Click(R.Find(window, "HideExtrusionsButton"));
            string dirty = R.Text(window, "DirtyBadgeText");
            R.Click(R.Find(window, "SaveExtrusionsButton"));
            Rhino.DocObjects.RhinoObject saved = doc.Objects.FindId(panel);
            string after = saved.Attributes.GetUserString(FrameAssignmentsKey) ?? string.Empty;
            ctx.Check("5.3 Save Extrusions after Hide keeps the profile assignment", after.Contains("1D-T000") && after.Contains("FRM-0"),
                new { hidden = segment.Id, dirtyAfterHide = dirty, before, after, frameConfig = saved.Attributes.GetUserString("UD_FRAME_CONFIG"),
                    status = R.Text(window, "FooterStatusText"), badge = R.Text(window, "DirtyBadgeText") });
            return true;
        }),
        new Step("5.4 back to Cladding view", ctx =>
        {
            object window = EditorWindow();
            R.Click(R.Find(window, "CladdingViewButton"));
            var state = Visibilities(window, "MaterialLegendSection", "CladdingAssignmentSection", "ClearCellsButton", "ExtrusionActions");
            ctx.Check("5.4 switching back restores the Cladding view", state["MaterialLegendSection"] == "Visible" &&
                state["CladdingAssignmentSection"] == "Visible" && state["ExtrusionActions"] != "Visible", state);
            return true;
        }),
        new Step("5.5 closing without edits asks nothing", ctx =>
        {
            object window = EditorWindow();
            // Hiding a mullion also changes the cells, so Save Extrusions left cladding changes; Save saves both.
            string before = R.Text(window, "DirtyBadgeText");
            R.Click(R.Find(window, "SaveButton"));
            string saved = R.Text(window, "DirtyBadgeText");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            R.Call(window, "Close");
            watch.Stop();
            ctx.Check("5.5 a saved editor closes without the discard prompt", saved == "Saved" && EditorWindow() == null,
                new { badgeBeforeSave = before, badgeAfterSave = saved, status = R.Text(window, "FooterStatusText"), seconds = watch.Elapsed.TotalSeconds,
                    note = "Watch-Dialogs.ps1 must log no prompt for this close (its first answer, No, would keep the editor open)." });
            SelectOnly(ctx.Doc, (Guid)ctx.Bag["panelF"]);
            ctx.Cmd("_PCeditor");
            ctx.Doc.Objects.UnselectAll();
            return true;
        }),
        new Step("5.5 closing with unsaved edits asks; No keeps the editor", ctx =>
        {
            object window = EditorWindow();
            R.Click(R.Find(window, "ExtrusionViewButton"));
            var segment = Segments(window).First(item => !item.IsFrame && item.Axis == "Horizontal");
            var selection = (System.Collections.Generic.ISet<string>)R.Get(window, "_selectedExtrusions");
            selection.Clear();
            selection.Add(segment.Id);
            R.Call(window, "UpdateExtrusionSelectionUi");
            R.Click(R.Find(window, "HideExtrusionsButton"));
            string badge = R.Text(window, "DirtyBadgeText");
            R.Call(window, "Close");   // the watcher answers No
            ctx.Check("5.5 No keeps the editor open with its edits", EditorWindow() != null && (bool)R.Get(window, "IsVisible") &&
                R.Text(window, "DirtyBadgeText") == "Unsaved", new { badgeBefore = badge });
            return true;
        }),
        new Step("5.5 Yes discards and closes", ctx =>
        {
            object window = EditorWindow() ?? throw new InvalidOperationException("The editor was already closed.");
            R.Call(window, "Close");   // the watcher answers Yes
            ctx.Check("5.5 Yes closes the editor", EditorWindow() == null, null);
            return true;
        }),
    };

    private sealed class SegmentInfo
    {
        public string Id = string.Empty;
        public string Axis = string.Empty;
        public bool IsFrame;
        public string Code = string.Empty;
    }

    private static List<SegmentInfo> Segments(object window) =>
        ((System.Collections.IEnumerable)R.Call(window, "CurrentSegments")).Cast<object>().Select(segment => new SegmentInfo
        {
            Id = (string)R.Get(segment, "Id"), Axis = R.Get(segment, "Axis").ToString(),
            IsFrame = (bool)R.Get(segment, "IsFrame"), Code = (string)R.Get(segment, "Code")
        }).ToList();

    private static Dictionary<string, string> Visibilities(object window, params string[] names) =>
        names.ToDictionary(name => name, name => R.Find(window, name) is object element ? R.Get(element, "Visibility").ToString() : "missing");

    // ---------------------------------------------------------------- PCweb (Wireframe Web)

    // Runs PCweb on the active saved model with the unsaved-changes answer given directly (its dialog
    // is not driven). Parameters.assembly names a PanelCladdingEditor build to load into its own load
    // context when that build is not the installed one; otherwise the loaded plug-in is used. Needs
    // Wireframe Web installed and a model whose project code is not a real project, since PCweb links
    // that project to the model. Every run that opens the model also opens a browser tab.
    private const string WebCommandType = "PanelCladdingEditor.UI.PanelCladdingWebCommand";
    private const string WebLauncherType = "PanelCladdingEditor.Infrastructure.WireframeWeb.WireframeWebLauncher";

    private static List<Step> WebSteps() => new List<Step>
    {
        new Step("W.0 setup", ctx =>
        {
            Type command = WebCommand(ctx);
            string launcher = command.Assembly.GetType(WebLauncherType, true)
                .GetMethod("FindLauncher", Type.EmptyTypes).Invoke(null, null) as string;
            string version = command.Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? string.Empty;
            RhinoDoc doc = ctx.Doc;
            ctx.Note("W.0 setup", new { assembly = Text(ctx.Parameters, "assembly"), version, launcher, model = doc.Path, port = WebPort() });
            ctx.Check("W.0 Wireframe Web is installed", launcher != null && System.IO.File.Exists(launcher), launcher);
            // Rhino opens a file read-only when it cannot lock it (for example a copy still being
            // scanned); W.3 would then stop on Rhino's "File Is Read-only" message.
            ctx.Check("W.0 the test model is saved, writable, unmodified and named <CODE> - ...", !string.IsNullOrEmpty(doc.Path) &&
                !doc.IsReadOnly && !doc.Modified && WebProject(doc).Length > 0, new { doc.Path, doc.IsReadOnly, doc.Modified });
            return true;
        }),
        new Step("W.1 saved model opens without a question", ctx =>
        {
            string result = WebOpen(ctx, "W.1", null, out bool asked);
            string added = WebHistory(ctx, "W.1");
            ctx.Check("W.1 PCweb starts the launcher and returns at once", result == "Success" && !asked &&
                added.Contains("PCweb: opening " + WebProject(ctx.Doc).ToUpperInvariant() + " in Wireframe Web..."), new { result, asked, added });
            return true;
        }),
        WebResultStep("W.1"),
        new Step("W.2 unsaved changes, LastSave", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            doc.Modified = true;
            DateTime before = System.IO.File.GetLastWriteTimeUtc(doc.Path);
            string result = WebOpen(ctx, "W.2", false, out bool asked);
            ctx.Check("W.2 PCweb asks, keeps the changes unsaved and opens the last save", result == "Success" && asked && doc.Modified &&
                System.IO.File.GetLastWriteTimeUtc(doc.Path) == before, new { result, asked, doc.Modified, before, added = WebHistory(ctx, "W.2") });
            string again = WebOpen(ctx, "W.2b", false, out bool askedAgain);
            string added = WebHistory(ctx, "W.2b");
            ctx.Check("W.2 a second run while the first waits starts nothing", again == "Nothing" && !askedAgain &&
                added.Contains("still opening") && !added.Contains("PCweb: opening"), new { again, askedAgain, added });
            return true;
        }),
        WebResultStep("W.2"),
        new Step("W.3 unsaved changes, Save", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            if (doc.IsReadOnly)
            {
                ctx.Check("W.3 PCweb asks, saves, then opens", false, "the test model is open read-only");
                return true;
            }
            doc.Modified = true;
            DateTime before = System.IO.File.GetLastWriteTimeUtc(doc.Path);
            string result = WebOpen(ctx, "W.3", true, out bool asked);
            DateTime after = System.IO.File.GetLastWriteTimeUtc(doc.Path);
            ctx.Check("W.3 PCweb asks, saves, then opens", result == "Success" && asked && !doc.Modified && after > before,
                new { result, asked, doc.Modified, before, after, added = WebHistory(ctx, "W.3") });
            return true;
        }),
        WebResultStep("W.3"),
        new Step("W.4 unsaved changes, Cancel", ctx =>
        {
            RhinoDoc doc = ctx.Doc;
            doc.Modified = true;
            DateTime before = System.IO.File.GetLastWriteTimeUtc(doc.Path);
            string result = WebOpen(ctx, "W.4", null, out bool asked);
            string added = WebHistory(ctx, "W.4");
            ctx.Check("W.4 Cancel saves nothing and starts nothing", result == "Cancel" && asked && doc.Modified &&
                System.IO.File.GetLastWriteTimeUtc(doc.Path) == before && !added.Contains("PCweb:"), new { result, asked, added });
            doc.Modified = false;
            return true;
        }),
    };

    // Waits for PCweb's result line, then reads the project's model status from Wireframe Web.
    private static Step WebResultStep(string id) => new Step(id + " result", ctx =>
    {
        string line = WebHistory(ctx, id).Split('\n').Select(item => item.Trim())
            .LastOrDefault(item => item.StartsWith("PCweb: ") && !item.StartsWith("PCweb: opening ") && !item.Contains("still opening"));
        if (line == null) return false;
        ctx.Note(id + " document at result", DocState(ctx.Doc));
        double seconds = Math.Round(((System.Diagnostics.Stopwatch)ctx.Bag[id + ".watch"]).Elapsed.TotalSeconds, 1);
        string project = WebProject(ctx.Doc);
        int at = line.IndexOf("http", StringComparison.Ordinal);
        string address = line.Contains(" is open in Wireframe Web: ") && at > 0 ? line.Substring(at) : string.Empty;
        ctx.Check(id + " PCweb reports the page address", address.Contains("source=api&project=" + project + "&user="), new { line, seconds });

        string status = WebGet("/api/projects/" + project + "/model-file");
        using (JsonDocument json = JsonDocument.Parse(status))
        {
            JsonElement root = json.RootElement;
            string fileName = root.TryGetProperty("fileName", out JsonElement name) ? name.GetString() : string.Empty;
            string loaded = root.TryGetProperty("loadedSavedAt", out JsonElement value) ? value.GetString() : string.Empty;
            DateTime saved = System.IO.File.GetLastWriteTimeUtc(ctx.Doc.Path);
            bool same = DateTime.TryParse(loaded, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out DateTime loadedAt) &&
                Math.Abs((loadedAt - saved).TotalSeconds) < 1;
            ctx.Check(id + " Wireframe Web loaded the saved file", fileName == System.IO.Path.GetFileName(ctx.Doc.Path) && same,
                new { fileName, loadedSavedAt = loaded, fileSavedAt = saved.ToString("o"), status });
        }
        return true;
    }, maxAttempts: 1200);

    // Calls PCweb's Open(doc, askToSave); returns the Rhino result's name.
    private static string WebOpen(Ctx ctx, string id, bool? answer, out bool asked)
    {
        System.Reflection.MethodInfo open = WebCommand(ctx).GetMethod("Open",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        bool wasAsked = false;
        var ask = new Func<RhinoDoc, bool?>(document => { wasAsked = true; return answer; });
        ctx.Bag[id + ".history"] = RhinoApp.CommandHistoryWindowText ?? string.Empty;
        ctx.Bag[id + ".watch"] = System.Diagnostics.Stopwatch.StartNew();
        string before = DocState(ctx.Doc);
        object result = open.Invoke(null, new object[] { ctx.Doc, ask });
        ctx.Note(id + " document", new { before, after = DocState(ctx.Doc) });
        asked = wasAsked;
        return result.ToString();
    }

    private static string DocState(RhinoDoc doc) =>
        DateTime.Now.ToString("HH:mm:ss.fff") + " readOnly=" + doc.IsReadOnly + " locked=" + doc.IsLocked + " modified=" + doc.Modified;

    private static string WebHistory(Ctx ctx, string id) =>
        Ctx.Added((string)ctx.Bag[id + ".history"], RhinoApp.CommandHistoryWindowText ?? string.Empty);

    private static Type WebCommand(Ctx ctx)
    {
        if (ctx.Bag.TryGetValue("web.command", out object cached)) return (Type)cached;
        string path = Text(ctx.Parameters, "assembly");
        Type type;
        if (path.Length > 0)
        {
            // From bytes, so the build output stays unlocked; its references resolve to the loaded ones.
            var context = new System.Runtime.Loader.AssemblyLoadContext("PcLive.web." + Guid.NewGuid().ToString("N"), true);
            type = context.LoadFromStream(new System.IO.MemoryStream(System.IO.File.ReadAllBytes(path))).GetType(WebCommandType, true);
        }
        else
        {
            type = R.T(WebCommandType) ?? throw new InvalidOperationException("The loaded plug-in has no PCweb.");
        }
        ctx.Bag["web.command"] = type;
        return type;
    }

    // Wireframe Web's project id: the code before the first " - ", in lower case.
    private static string WebProject(RhinoDoc doc)
    {
        string name = System.IO.Path.GetFileNameWithoutExtension(doc.Path ?? string.Empty);
        int separator = name.IndexOf(" - ", StringComparison.Ordinal);
        return separator > 0 ? new string(name.Substring(0, separator).Trim().TakeWhile(char.IsLetterOrDigit).ToArray()).ToLowerInvariant() : string.Empty;
    }

    private static string WebPort()
    {
        using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\IEF\Wireframe Web"))
            return key?.GetValue("Port") as string ?? "8795";
    }

    private static string WebGet(string path)
    {
        using (var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) })
        {
            client.DefaultRequestHeaders.Add("x-wf-user", System.Environment.UserName);
            string url = "http://127.0.0.1:" + WebPort() + path;
            return System.Threading.Tasks.Task.Run(() => client.GetStringAsync(url)).Result;
        }
    }

    // ---------------------------------------------------------------- probe

    // Runs Parameters.macro once (to try a macro shape before using it in a suite).
    private static List<Step> ProbeSteps() => new List<Step>
    {
        new Step("probe", ctx =>
        {
            string select = Text(ctx.Parameters, "select");
            if (select.Length > 0) SelectOnly(ctx.Doc, select.Split(',').Select(Guid.Parse).ToArray());
            ctx.Note("history", ctx.Cmd(Text(ctx.Parameters, "macro")));
            ctx.Note("selected", ctx.Doc.Objects.GetSelectedObjects(false, false).Select(item => item.Id).ToArray());
            return true;
        }),
    };

    // ---------------------------------------------------------------- reflection (WPF and the plug-in are not referenced)

    private static class R
    {
        private const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

        public static Type T(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(fullName, false)).FirstOrDefault(type => type != null);

        public static object StaticField(string typeName, string field) =>
            T(typeName)?.GetField(field, Any)?.GetValue(null);

        public static object Get(object target, string name)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                System.Reflection.PropertyInfo property = type.GetProperty(name, Any | System.Reflection.BindingFlags.DeclaredOnly);
                if (property != null && property.GetIndexParameters().Length == 0) return property.GetValue(target);
                System.Reflection.FieldInfo field = type.GetField(name, Any | System.Reflection.BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(target);
            }
            throw new MissingMemberException(target.GetType().FullName, name);
        }

        public static void Set(object target, string name, object value)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                System.Reflection.PropertyInfo property = type.GetProperty(name, Any | System.Reflection.BindingFlags.DeclaredOnly);
                if (property != null) { property.SetValue(target, value); return; }
            }
            throw new MissingMemberException(target.GetType().FullName, name);
        }

        public static object Call(object target, string name, params object[] args)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                System.Reflection.MethodInfo method = type.GetMethods(Any | System.Reflection.BindingFlags.DeclaredOnly)
                    .FirstOrDefault(item => item.Name == name && item.GetParameters().Length == args.Length);
                if (method != null) return method.Invoke(target, args);
            }
            throw new MissingMethodException(target.GetType().FullName, name);
        }

        public static object Find(object window, string name) => Call(window, "FindName", name);

        public static string Text(object window, string name) => Find(window, name) is object element ? (string)Get(element, "Text") ?? string.Empty : string.Empty;

        // Raises Click the way a mouse click does, so the button's handler and its bindings run.
        public static void Click(object button)
        {
            object clickEvent = T("System.Windows.Controls.Primitives.ButtonBase").GetField("ClickEvent").GetValue(null);
            object args = Activator.CreateInstance(T("System.Windows.RoutedEventArgs"), clickEvent, button);
            Call(button, "RaiseEvent", args);
        }

        // Renders a window's content off screen to a PNG at 96 dpi.
        public static void Render(object window, string path)
        {
            Call(window, "UpdateLayout");   // apply visibility changes made in the same step
            object content = Get(window, "Content");
            double width = (double)Get(content, "ActualWidth"), height = (double)Get(content, "ActualHeight");
            object format = T("System.Windows.Media.PixelFormats").GetProperty("Pbgra32").GetValue(null);
            object bitmap = Activator.CreateInstance(T("System.Windows.Media.Imaging.RenderTargetBitmap"),
                (int)Math.Ceiling(width), (int)Math.Ceiling(height), 96d, 96d, format);
            Call(bitmap, "Render", content);
            object encoder = Activator.CreateInstance(T("System.Windows.Media.Imaging.PngBitmapEncoder"));
            object frame = T("System.Windows.Media.Imaging.BitmapFrame").GetMethods()
                .First(method => method.Name == "Create" && method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType.Name == "BitmapSource").Invoke(null, new[] { bitmap });
            ((System.Collections.IList)Get(encoder, "Frames")).Add(frame);
            using (var stream = System.IO.File.Create(path)) Call(encoder, "Save", stream);
        }
    }

    // ---------------------------------------------------------------- model helpers

    private static Rhino.DocObjects.ObjectEnumeratorSettings Everything(Rhino.DocObjects.ObjectType filter) =>
        new Rhino.DocObjects.ObjectEnumeratorSettings
        {
            NormalObjects = true, LockedObjects = true, HiddenObjects = true, ActiveObjects = true,
            ReferenceObjects = false, IncludeLights = false, ObjectTypeFilter = filter
        };

    private static string LayerPath(RhinoDoc doc, Rhino.DocObjects.RhinoObject item) =>
        item.Attributes.LayerIndex >= 0 ? doc.Layers[item.Attributes.LayerIndex].FullPath : string.Empty;

    private static bool Under(string path, string root) =>
        string.Equals(path, root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + "::", StringComparison.OrdinalIgnoreCase);

    private static string Pid(Rhino.DocObjects.RhinoObject item) => (item.Attributes.GetUserString(PidKey) ?? string.Empty).Trim();

    private static string Cid(Rhino.DocObjects.RhinoObject item) => (item.Attributes.GetUserString(CidKey) ?? string.Empty).Trim();

    private static List<Rhino.DocObjects.RhinoObject> PanelsOn(RhinoDoc doc, string layerPath) =>
        doc.Objects.GetObjectList(Everything(Rhino.DocObjects.ObjectType.Brep))
            .Where(item => string.Equals(LayerPath(doc, item), layerPath, StringComparison.OrdinalIgnoreCase))
            .OrderBy(Pid, StringComparer.Ordinal).ToList();

    // Managed dependencies carry the panel's PID and a CID that starts with the panel's CID.
    private static List<Rhino.DocObjects.RhinoObject> Managed(RhinoDoc doc, Guid panelId, string root, Rhino.DocObjects.ObjectType filter)
    {
        Rhino.DocObjects.RhinoObject panel = doc.Objects.FindId(panelId);
        string pid = Pid(panel), cid = Cid(panel);
        return doc.Objects.GetObjectList(Everything(filter))
            .Where(item => item.Id != panelId && Under(LayerPath(doc, item), root) &&
                string.Equals(Pid(item), pid, StringComparison.OrdinalIgnoreCase) &&
                Cid(item).StartsWith(cid, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Attributes.GetUserString("CRV") ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(item => item.Id).ToList();
    }

    private static List<Rhino.DocObjects.RhinoObject> ManagedSurfaces(RhinoDoc doc, Guid panelId) =>
        Managed(doc, panelId, SurfaceBranch, Rhino.DocObjects.ObjectType.Brep);

    private static List<Rhino.DocObjects.RhinoObject> ManagedCurves(RhinoDoc doc, Guid panelId) =>
        Managed(doc, panelId, CurveBranch, Rhino.DocObjects.ObjectType.Curve);

    private static int DeleteManaged(RhinoDoc doc, Rhino.DocObjects.RhinoObject panel)
    {
        int count = 0;
        foreach (var item in ManagedSurfaces(doc, panel.Id).Concat(ManagedCurves(doc, panel.Id)))
        {
            if (item.IsLocked) doc.Objects.Unlock(item.Id, true);
            if (item.IsHidden) doc.Objects.Show(item.Id, true);
            if (doc.Objects.Delete(item.Id, true)) count++;
        }
        return count;
    }

    private static string F(double value) => value.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture);

    private static double UserNumber(Rhino.DocObjects.RhinoObject item, string key) =>
        double.Parse((item.Attributes.GetUserString(key) ?? "0").Trim(), System.Globalization.CultureInfo.InvariantCulture);

    // One horizontal and one vertical mullion at the given fractions of height and width, and the cell logic.
    private static void WriteLayout(RhinoDoc doc, Guid panelId, double heightFraction, double widthFraction, Dictionary<string, string> logic)
    {
        Rhino.DocObjects.RhinoObject panel = doc.Objects.FindId(panelId);
        var attributes = panel.Attributes.Duplicate();
        foreach (string key in attributes.GetUserStrings().AllKeys.Where(key =>
            key.StartsWith("CW_2.03_OFFSET_H", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("CW_2.04_OFFSET_V", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            attributes.DeleteUserString(key);
        }
        attributes.SetUserString("CW_2.03_OFFSET_H0", F(Math.Round(UserNumber(panel, "CW_2.02_UNIT_HEIGHT") * heightFraction, 3)));
        attributes.SetUserString("CW_2.04_OFFSET_V0", F(Math.Round(UserNumber(panel, "CW_2.01_UNIT_WIDTH") * widthFraction, 3)));
        attributes.SetUserString(LogicKey, JsonSerializer.Serialize(logic));
        // The frame configuration must describe the same grid (the XYZ panels carry "0H 0V" in CW_9.02).
        attributes.DeleteUserString("CW_9.02_FRAME_CONFIG");
        attributes.SetUserString("UD_FRAME_CONFIG", "1H 1V");
        doc.Objects.ModifyAttributes(panel, attributes, true);
    }

    private static void SelectOnly(RhinoDoc doc, params Guid[] ids)
    {
        doc.Objects.UnselectAll();
        foreach (Guid id in ids) doc.Objects.Select(id, true, true);
        doc.Views.Redraw();
    }

    private static bool IsSelected(RhinoDoc doc, Guid id) => doc.Objects.FindId(id)?.IsSelected(false) > 0;

    private static string ColorText(Rhino.DocObjects.ObjectAttributes attributes) =>
        attributes.ObjectColor.R + "," + attributes.ObjectColor.G + "," + attributes.ObjectColor.B;

    private static string UserTextText(Rhino.DocObjects.ObjectAttributes attributes)
    {
        var text = attributes.GetUserStrings();
        return string.Join(";", text.AllKeys.OrderBy(key => key, StringComparer.Ordinal).Select(key => key + "=" + text[key]));
    }

    private static string GeometryText(Rhino.Geometry.GeometryBase geometry)
    {
        Rhino.Geometry.BoundingBox box = geometry.GetBoundingBox(true);
        string size = geometry is Rhino.Geometry.Curve curve ? F(Math.Round(curve.GetLength(), 4))
            : geometry is Rhino.Geometry.Brep brep ? F(Math.Round(brep.GetArea(), 3)) : string.Empty;
        return F(Math.Round(box.Min.X, 4)) + "," + F(Math.Round(box.Min.Y, 4)) + "," + F(Math.Round(box.Min.Z, 4)) + "/" +
            F(Math.Round(box.Max.X, 4)) + "," + F(Math.Round(box.Max.Y, 4)) + "," + F(Math.Round(box.Max.Z, 4)) + "/" + size;
    }

    private static string ObjectState(RhinoDoc doc, Rhino.DocObjects.RhinoObject item) =>
        item.Id + "|" + LayerPath(doc, item) + "|" + item.Attributes.ColorSource + ":" + ColorText(item.Attributes) + "|" +
        (item.IsLocked ? "L" : "") + (item.IsHidden ? "H" : "") + "|" + GeometryText(item.Geometry) + "|" + UserTextText(item.Attributes);

    private static string CurveState(List<Rhino.DocObjects.RhinoObject> curves) =>
        string.Join("\n", curves.OrderBy(item => item.Id).Select(item => ObjectState(item.Document, item)));

    private static string SurfaceState(List<Rhino.DocObjects.RhinoObject> surfaces) =>
        string.Join("\n", surfaces.OrderBy(item => item.Id).Select(item => ObjectState(item.Document, item)));

    // Count and geometry of every object on a branch, to show that a run left that branch alone.
    private static string BranchFingerprint(RhinoDoc doc, string root, Rhino.DocObjects.ObjectType filter)
    {
        var items = doc.Objects.GetObjectList(Everything(filter)).Where(item => Under(LayerPath(doc, item), root)).ToList();
        return items.Count + ":" + string.Join(";", items.OrderBy(item => item.Id).Select(item => item.Id + "@" + GeometryText(item.Geometry))).GetHashCode();
    }

    private static string CurveFingerprint(RhinoDoc doc) => BranchFingerprint(doc, CurveBranch, Rhino.DocObjects.ObjectType.Curve);

    private static string SurfaceFingerprint(RhinoDoc doc) => BranchFingerprint(doc, SurfaceBranch, Rhino.DocObjects.ObjectType.Brep);

    private static string Text(JsonElement parameters, string name) =>
        parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty(name, out JsonElement value) ? value.GetString() ?? string.Empty : string.Empty;

    // ---------------------------------------------------------------- runner

    private sealed class Step
    {
        public Step(string name, Func<Ctx, bool> body, int maxAttempts = 1)
        {
            Name = name;
            Body = body;
            MaxAttempts = maxAttempts;
        }

        public string Name { get; }
        public Func<Ctx, bool> Body { get; }
        public int MaxAttempts { get; }
    }

    private sealed class Ctx
    {
        public Ctx(string suite, uint serial, string evidenceDir, JsonElement parameters)
        {
            Suite = suite;
            Serial = serial;
            EvidenceDir = evidenceDir;
            Parameters = parameters.Clone();
        }

        public string Suite { get; }
        public uint Serial { get; set; }
        public string EvidenceDir { get; }
        public JsonElement Parameters { get; }
        public string CurrentStep { get; set; } = string.Empty;
        public DateTime StepStarted { get; set; }
        public RhinoDoc Doc => RhinoDoc.FromRuntimeSerialNumber(Serial) ?? RhinoDoc.ActiveDoc;
        public List<Dictionary<string, object>> Checks { get; } = new List<Dictionary<string, object>>();
        public Dictionary<string, object> Notes { get; } = new Dictionary<string, object>();
        public List<Dictionary<string, object>> Commands { get; } = new List<Dictionary<string, object>>();
        public Dictionary<string, object> Bag { get; } = new Dictionary<string, object>();

        // Runs a macro as typed input and returns what it added to the command history.
        public string Cmd(string macro)
        {
            string before = RhinoApp.CommandHistoryWindowText ?? string.Empty;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            bool ok = RhinoApp.RunScript(Serial, macro, true);
            watch.Stop();
            string after = RhinoApp.CommandHistoryWindowText ?? string.Empty;
            string added = Added(before, after);
            Commands.Add(new Dictionary<string, object>
            {
                ["step"] = CurrentStep, ["macro"] = macro, ["result"] = ok, ["seconds"] = Math.Round(watch.Elapsed.TotalSeconds, 3), ["history"] = added
            });
            return added;
        }

        // The history window drops its oldest lines when it grows long, so the new text is found after
        // the last stretch of the old text rather than by length.
        public static string Added(string before, string after)
        {
            if (after.StartsWith(before, StringComparison.Ordinal)) return after.Substring(before.Length);
            string tail = before.Length > 400 ? before.Substring(before.Length - 400) : before;
            int at = tail.Length == 0 ? -1 : after.LastIndexOf(tail, StringComparison.Ordinal);
            return at >= 0 ? after.Substring(at + tail.Length) : after;
        }

        public void Check(string id, bool pass, object evidence) =>
            Checks.Add(new Dictionary<string, object> { ["id"] = id, ["pass"] = pass, ["step"] = CurrentStep, ["evidence"] = evidence });

        public void Note(string key, object value) => Notes[key] = value;

        public string EvidenceFile(string name)
        {
            System.IO.Directory.CreateDirectory(EvidenceDir);
            return System.IO.Path.Combine(EvidenceDir, name);
        }
    }

    private sealed class Runner
    {
        private readonly Ctx _ctx;
        private readonly List<Step> _steps;
        private readonly DateTime _started = DateTime.Now;
        private int _index;
        private int _attempts;
        private bool _busy;
        private bool _finished;
        private string _error = string.Empty;
        private System.Threading.Timer _poke;

        public Runner(Ctx ctx, List<Step> steps)
        {
            _ctx = ctx;
            _steps = steps;
        }

        public void Begin()
        {
            Publish("queued");
            RhinoApp.Idle += OnIdle;
            // Idle is raised after window messages; a no-op message every 250 ms keeps it coming.
            _poke = new System.Threading.Timer(_ => RhinoApp.InvokeOnUiThread(new Action(() => { })), null, 250, 250);
        }

        private void OnIdle(object sender, EventArgs args)
        {
            if (_busy || _finished || Rhino.Commands.Command.InCommand()) return;
            _busy = true;
            try
            {
                if (_index >= _steps.Count)
                {
                    Finish("done");
                    return;
                }
                Step step = _steps[_index];
                if (_attempts == 0) _ctx.StepStarted = DateTime.Now;
                _ctx.CurrentStep = step.Name;
                bool complete;
                try
                {
                    complete = step.Body(_ctx);
                }
                catch (Exception exception)
                {
                    _ctx.Check(step.Name + " raised no exception", false, exception.ToString());
                    complete = true;
                }
                _attempts++;
                if (!complete && _attempts >= step.MaxAttempts)
                {
                    _ctx.Check(step.Name + " finished in time", false, _attempts + " attempts");
                    complete = true;
                }
                if (complete)
                {
                    _index++;
                    _attempts = 0;
                }
                Publish("running");
            }
            catch (Exception exception)
            {
                _error = exception.ToString();
                Finish("error");
            }
            finally
            {
                _busy = false;
            }
        }

        private void Finish(string status)
        {
            _finished = true;
            RhinoApp.Idle -= OnIdle;
            _poke?.Dispose();
            Publish(status);
        }

        private void Publish(string status)
        {
            var state = new Dictionary<string, object>
            {
                ["suite"] = _ctx.Suite,
                ["status"] = status,
                ["started"] = _started.ToString("s"),
                ["updated"] = DateTime.Now.ToString("s"),
                ["step"] = _index < _steps.Count ? _steps[_index].Name : string.Empty,
                ["passed"] = _ctx.Checks.Count(check => (bool)check["pass"]),
                ["failed"] = _ctx.Checks.Count(check => !(bool)check["pass"]),
                ["error"] = _error,
                ["checks"] = _ctx.Checks,
                ["notes"] = _ctx.Notes,
                ["commands"] = _ctx.Commands
            };
            string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            AppDomain.CurrentDomain.SetData(StatePrefix + _ctx.Suite, json);
            if (status != "running" && _ctx.EvidenceDir.Length > 0)
            {
                System.IO.Directory.CreateDirectory(_ctx.EvidenceDir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(_ctx.EvidenceDir, _ctx.Suite + ".json"), json);
            }
        }
    }
}
