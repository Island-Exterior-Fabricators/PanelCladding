using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DocumentFormat.OpenXml.Packaging;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using PanelCladdingEditor.UI;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace PcReportCommandSmoke;

// PCreport (1.3.0): every rule fires on a BKT-like fixture, a compliant panel and its surface and
// curve raise nothing, not-tracked panels are skipped, export round-trips, and the window shows the
// counts, rule groups and issues and selects objects for the chosen rows.
internal static class Program
{
    private const string DocumentPath = @"C:\Projects\BKT\BKT - Wireframe.3dm";
    private const string Root = "BKT-IEF-Wireframe";
    private const string PanelBranch = Root + "::Surfaces - PNL";
    private const string PanelLayer = PanelBranch + "::Surfaces - WT_01";
    private const string NotInScope = PanelBranch + "::Surfaces - Not in Scope";
    private const string NearMiss = Root + "::Surfaces-PNL";
    private const string CurveLayer = Root + "::Curves - PNL::Curves - WT_01";
    private const string OrphanCurveLayer = Root + "::Curves - PNL::Curves - WT_09";
    private const string WireframeGlass = Root + "::Surfaces - GLS::GLS-001";
    private const string StepGlass = "Reference::Surfaces - STEP::Surfaces - GLS::GLS-001";
    private const string StepMismatch = "Reference::Surfaces - STEP::Surfaces - GLS::MPL-002";

    private static readonly Guid Clean = Id(1), Messy = Id(2), BadUnit = Id(3), DupA = Id(4), DupB = Id(5);
    private static readonly Guid Unreadable = Id(6), Unbaked = Id(7), OnBranch = Id(8), Excluded = Id(9), OffBranch = Id(10);
    private static readonly Guid CleanSurface = Id(21), CleanCurve = Id(22), Orphan = Id(23), DupSurfaceA = Id(24);
    private static readonly Guid DupSurfaceB = Id(25), CladdingOff = Id(26), CurveOff = Id(27), ExcludedSurface = Id(28);
    private static readonly Guid GlassCopy = Id(29);

    [STAThread]
    private static int Main()
    {
        try
        {
            PanelCladdingReport report = new PanelCladdingComplianceService().Check(BuildSnapshot(), new DateTime(2026, 10, 31, 12, 0, 0));
            VerifyRules(report);
            VerifyCleanAndNotTracked(report);
            VerifyProjectRoot();
            VerifyExport(report);
            VerifyWindow(report);
            VerifyReadOnlyReader();
            Console.WriteLine("PASS: PCreport command regression");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"PCreport smoke failed: {ex}");
            return 1;
        }
    }

    private static void VerifyRules(PanelCladdingReport report)
    {
        var expected = new (PanelCladdingReportRule Rule, Guid ObjectId, string Layer)[]
        {
            (PanelCladdingComplianceService.RetiredLayerTree, Guid.Empty, "01_CW Panels"),
            (PanelCladdingComplianceService.PanelWithoutTypeLayer, OnBranch, PanelBranch),
            (PanelCladdingComplianceService.NearStandardName, Guid.Empty, NearMiss),
            (PanelCladdingComplianceService.StepFamilyMismatch, Guid.Empty, StepMismatch),
            (PanelCladdingComplianceService.CurveLayerWithoutPanelLayer, Guid.Empty, OrphanCurveLayer),
            (PanelCladdingComplianceService.NotTrackedLayer, Guid.Empty, NotInScope),
            (PanelCladdingComplianceService.KeyCaseDuplicate, Messy, PanelLayer),
            (PanelCladdingComplianceService.DuplicateCid, DupA, PanelLayer),
            (PanelCladdingComplianceService.DuplicateCid, DupB, PanelLayer),
            (PanelCladdingComplianceService.UnreadableData, Unreadable, PanelLayer),
            (PanelCladdingComplianceService.PidFormat, Messy, PanelLayer),
            (PanelCladdingComplianceService.CidMismatch, Messy, PanelLayer),
            (PanelCladdingComplianceService.NameMismatch, Messy, PanelLayer),
            (PanelCladdingComplianceService.UnitTypeInvalid, BadUnit, PanelLayer),
            (PanelCladdingComplianceService.RoleMismatch, Messy, PanelLayer),
            (PanelCladdingComplianceService.FrameTypePayload, Messy, PanelLayer),
            (PanelCladdingComplianceService.MissingSetupKeys, Messy, PanelLayer),
            (PanelCladdingComplianceService.LegacyKeys, Messy, PanelLayer),
            (PanelCladdingComplianceService.RoleCase, Messy, PanelLayer),
            (PanelCladdingComplianceService.PlaneMissing, Messy, PanelLayer),
            (PanelCladdingComplianceService.OrphanDependency, Orphan, StepGlass),
            (PanelCladdingComplianceService.NotTrackedOwner, ExcludedSurface, StepGlass),
            (PanelCladdingComplianceService.DuplicateDependencyCid, DupSurfaceA, StepGlass),
            (PanelCladdingComplianceService.DuplicateDependencyCid, DupSurfaceB, StepGlass),
            (PanelCladdingComplianceService.CladdingLayerMismatch, CladdingOff, StepGlass),
            (PanelCladdingComplianceService.CurveLayerMismatch, CurveOff, OrphanCurveLayer),
            (PanelCladdingComplianceService.PanelNotBaked, Unbaked, PanelLayer)
        };
        foreach ((PanelCladdingReportRule rule, Guid objectId, string layer) in expected)
        {
            Require(report.Issues.Any(issue => issue.Rule == rule && issue.ObjectId == objectId &&
                    string.Equals(issue.LayerPath, layer, StringComparison.Ordinal)),
                $"{rule.Id} {rule.Title} did not fire for {objectId} on {layer}.");
        }
        Require(PanelCladdingComplianceService.Rules
                .Where(rule => rule != PanelCladdingComplianceService.ProjectRootMissing)
                .All(rule => expected.Any(item => item.Rule == rule)),
            "The fixture must exercise every rule except the missing-root case (checked separately).");
        Require(PanelCladdingComplianceService.Rules.Select(rule => rule.Id).Distinct().Count() == PanelCladdingComplianceService.Rules.Count,
            "Rule IDs must be unique.");
        Require(report.Issues.Where(issue => issue.Rule == PanelCladdingComplianceService.MissingSetupKeys)
                .All(issue => issue.Rule.Severity == PanelCladdingReportSeverity.Info),
            "Missing keys are not errors.");
        Require(report.PanelCount == 8 && report.SurfaceCount == 6 && report.CurveCount == 2,
            $"Counts: {report.PanelCount} panels, {report.SurfaceCount} surfaces, {report.CurveCount} curves.");
        Console.WriteLine($"[OK] all {PanelCladdingComplianceService.Rules.Count} rules fire on the fixture; missing keys are Info");
    }

    private static void VerifyCleanAndNotTracked(PanelCladdingReport report)
    {
        PanelCladdingReportIssue? noisy = report.Issues.FirstOrDefault(issue =>
            issue.ObjectId == Clean || issue.ObjectId == CleanSurface || issue.ObjectId == CleanCurve);
        Require(noisy is null, $"A compliant panel, surface or curve was reported: {noisy?.Rule.Id} {noisy?.Detail}");
        Require(!report.Issues.Any(issue => issue.ObjectId == Excluded),
            "A panel on a Not in Scope layer must not be checked.");
        Require(!report.Issues.Any(issue => issue.ObjectId == GlassCopy || issue.ObjectId == OffBranch),
            "Objects with a PID outside the managed layers (a Surfaces - GLS copy, a Brep on a near-miss layer) must not be reported.");
        Console.WriteLine("[OK] a compliant panel, surface and curve raise nothing; not-tracked panels and unmanaged objects are skipped");
    }

    private static void VerifyProjectRoot()
    {
        PanelCladdingReport report = new PanelCladdingComplianceService().Check(new PanelCladdingReportSnapshot
        {
            DocumentPath = DocumentPath,
            LayerPaths = ["Other", "XYZ-IEF-Wireframe"]
        }, DateTime.Now);
        PanelCladdingReportIssue missing = report.Issues.Single(issue => issue.Rule == PanelCladdingComplianceService.ProjectRootMissing);
        Require(missing.Detail.Contains("BKT-IEF-Wireframe", StringComparison.Ordinal) &&
                missing.Rule.Severity == PanelCladdingReportSeverity.Error,
            "The project root comes from the file name and its absence is an error.");
        Console.WriteLine("[OK] a model without its <PROJECT>-IEF-Wireframe root is an error");
    }

    private static void VerifyExport(PanelCladdingReport report)
    {
        string path = Path.Combine(RegressionPaths.Output("pcreport-command"), "BKT - Wireframe - PC Report.xlsx");
        OperationResponse written = OpenXmlPanelCladdingReportWriter.Write(path, report);
        Require(written.Success, written.Message);
        using SpreadsheetDocument document = SpreadsheetDocument.Open(path, false);
        WorkbookPart workbook = document.WorkbookPart!;
        S.Sheet[] sheets = workbook.Workbook!.Sheets!.Elements<S.Sheet>().ToArray();
        Require(sheets.Select(sheet => sheet.Name!.Value).SequenceEqual(
                [OpenXmlPanelCladdingReportWriter.SummarySheetName, OpenXmlPanelCladdingReportWriter.IssuesSheetName]),
            "Export must contain Summary and Issues sheets.");
        var issues = (WorksheetPart)workbook.GetPartById(sheets[1].Id!.Value!);
        S.Row[] rows = issues.Worksheet!.Descendants<S.Row>().ToArray();
        Require(rows.Length == report.Issues.Count + 1, $"Issues sheet has {rows.Length} rows for {report.Issues.Count} issues.");
        string[] header = rows[0].Elements<S.Cell>().Select(cell => cell.InnerText).ToArray();
        Require(header.SequenceEqual(OpenXmlPanelCladdingReportWriter.IssueColumns), "Issues header changed.");
        Require(!File.Exists(path + ".writing"), "The temporary export file must not remain.");
        Console.WriteLine($"[OK] export writes Summary and {report.Issues.Count} issue rows");
    }

    private static void VerifyWindow(PanelCladdingReport report)
    {
        var navigator = new RecordingNavigator();
        var window = new PanelCladdingReportWindow(
            () => OperationResponse<PanelCladdingReport>.Ok(report),
            navigator,
            (_, _) => OperationResponse.Ok());
        Require(window.Rescan().Success && ReferenceEquals(window.Report, report), "Rescan must show the scanned report.");

        string errors = Named<TextBlock>(window, "ErrorCountText").Text;
        string warnings = Named<TextBlock>(window, "WarningCountText").Text;
        Require(errors == $"{report.Count(PanelCladdingReportSeverity.Error)} errors" &&
                warnings == $"{report.Count(PanelCladdingReportSeverity.Warning)} warnings",
            $"Header counts: {errors}, {warnings}.");

        ListBox rules = Named<ListBox>(window, "RuleList");
        int ruleCount = report.Issues.Select(issue => issue.Rule).Distinct().Count();
        Require(rules.Items.Count == ruleCount && rules.Items.Groups?.Count == 3,
            $"Rule list shows {rules.Items.Count} rules in {rules.Items.Groups?.Count} groups.");
        Require(rules.SelectedItem is PanelCladdingReportWindow.RuleRow first &&
                first.Rule == report.Issues[0].Rule &&
                Named<TextBlock>(window, "RuleTitleText").Text == first.Title,
            "The first rule must be selected and shown.");

        rules.SelectedItem = rules.Items.OfType<PanelCladdingReportWindow.RuleRow>()
            .Single(row => row.Rule == PanelCladdingComplianceService.DuplicateCid);
        ListView issues = Named<ListView>(window, "IssueList");
        Require(issues.Items.Count == 2, "Duplicate CID must list both panels.");
        issues.SelectAll();
        Require(navigator.Calls.Count == 1 && navigator.Calls[0].ToHashSet().SetEquals([DupA, DupB]),
            "Selecting issue rows must select and zoom their objects.");

        rules.SelectedItem = rules.Items.OfType<PanelCladdingReportWindow.RuleRow>()
            .Single(row => row.Rule == PanelCladdingComplianceService.NotTrackedLayer);
        issues.SelectAll();
        Require(navigator.Calls.Count == 1, "Layer-level rows have no object to select.");

        rules.SelectedItem = rules.Items.OfType<PanelCladdingReportWindow.RuleRow>()
            .Single(row => row.Rule == PanelCladdingComplianceService.CidMismatch);
        Render((FrameworkElement)window.Content, 1280, 800, "pcreport-window-1280x800.png");
        GridViewColumnCollection columns = ((GridView)issues.View).Columns;
        double columnsWidth = columns.Sum(column => column.ActualWidth);
        Require(columnsWidth <= issues.ActualWidth && columns[2].ActualWidth >= 240d &&
                columnsWidth >= issues.ActualWidth - SystemParameters.VerticalScrollBarWidth - 20d,
            $"Object, Layer and Detail must fill the issue list without running past it: columns {columnsWidth:0}, list {issues.ActualWidth:0}.");
        Render((FrameworkElement)window.Content, 1180, 760, "pcreport-window-1180x760.png");
        double defaultWidth = columns.Sum(column => column.ActualWidth);
        Require(defaultWidth <= issues.ActualWidth,
            $"At the default window size the Detail column must fit: columns {defaultWidth:0}, list {issues.ActualWidth:0}.");
        window.ShowReport(new PanelCladdingReport { DocumentPath = DocumentPath });
        Require(Named<TextBlock>(window, "EmptyText").Visibility == Visibility.Visible, "An empty report shows No issues.");
        Console.WriteLine("[OK] window shows counts, grouped rules and issues; rows select their objects");
    }

    private static void VerifyReadOnlyReader()
    {
        string source = File.ReadAllText(Path.Combine(RegressionPaths.RepositoryRoot,
            "src", "PanelCladdingEditor", "Infrastructure", "Rhino", "Live", "PanelCladding", "LivePanelCladdingReportReader.cs"));
        foreach (string token in new[] { "Objects.Add", "Objects.Replace", "Objects.Delete", "Objects.ModifyAttributes", "Layers.Add", "SetUserString", "BeginUndoRecord" })
        {
            Require(!source.Contains(token, StringComparison.Ordinal), $"The report reader must not write: {token}");
        }
        Console.WriteLine("[OK] the report reader only reads the model");
    }

    private static PanelCladdingReportSnapshot BuildSnapshot()
    {
        var objects = new List<PanelCladdingReportObject>
        {
            Panel(Clean, "N1-01-01", PanelLayer, "PID-BKT-N1-01-01", logic: "{\"0A\":\"GLS-001\"}"),
            Panel(Messy, "WRONG", PanelLayer, "PID-XYZ-N1-01-02", cid: "CID-XYZ-N1-01-02_P", role: "Child",
                extra: new() { ["cw_1.05_lot"] = "01", [PanelCladdingKeyService.FrameTypeKey] = "{\"FRM-0\":[]}", ["Plane"] = "x", ["AUTO_panel"] = "1" },
                remove: ["CW_7.00_BLIND_DOUBLE", PanelCladdingKeyService.PlaneKey]),
            Panel(BadUnit, "N1-01-03", PanelLayer, "PID-BKT-N1-01-03", unit: "X"),
            Panel(DupA, "N1-01-04", PanelLayer, "PID-BKT-N1-01-04"),
            Panel(DupB, "N1-01-04", PanelLayer, "PID-BKT-N1-01-04"),
            Panel(Unreadable, "N1-01-06", PanelLayer, "PID-BKT-N1-01-06", logic: "{\"0A\":\"GLS-001\",\"1A\":\"GLS-001\"}"),
            Panel(Unbaked, "N1-01-07", PanelLayer, "PID-BKT-N1-01-07", logic: "{\"0A\":\"STN-001\"}"),
            Panel(OnBranch, "N1-01-08", PanelBranch, "PID-BKT-N1-01-08"),
            Panel(Excluded, "N9-01-01", NotInScope, "PID-BKT-N9-01-01", unit: "X"),
            Panel(OffBranch, "N1-01-10", NearMiss, "PID-BKT-N1-01-10"),
            Dependency(CleanSurface, StepGlass, "PID-BKT-N1-01-01", "CID-BKT-N1-01-01_0A", "GLS-001"),
            Dependency(CleanCurve, CurveLayer, "PID-BKT-N1-01-01", "CID-BKT-N1-01-01_FRM-0", curve: true),
            Dependency(Orphan, StepGlass, "PID-BKT-ZZ-99-99", "CID-BKT-ZZ-99-99_0A", "GLS-001"),
            Dependency(DupSurfaceA, StepGlass, "PID-BKT-N1-01-03", "CID-BKT-N1-01-03_0A", "GLS-001"),
            Dependency(DupSurfaceB, StepGlass, "PID-BKT-N1-01-03", "CID-BKT-N1-01-03_0A", "GLS-001"),
            Dependency(CladdingOff, StepGlass, "PID-BKT-N1-01-08", "CID-BKT-N1-01-08_0A", "STN-001"),
            Dependency(CurveOff, OrphanCurveLayer, "PID-BKT-N1-01-03", "CID-BKT-N1-01-03_FRM-0", curve: true),
            Dependency(ExcludedSurface, StepGlass, "PID-BKT-N9-01-01", "CID-BKT-N9-01-01_0A", "GLS-001"),
            Dependency(GlassCopy, WireframeGlass, "PID-BKT-N1-01-01", "CID-BKT-N1-01-01_0A", "GLS-001")
        };
        return new PanelCladdingReportSnapshot
        {
            DocumentPath = DocumentPath,
            Tolerance = 0.001d,
            LayerPaths =
            [
                Root, PanelBranch, PanelLayer, NotInScope, NearMiss, Root + "::Curves - PNL", CurveLayer, OrphanCurveLayer,
                Root + "::Surfaces - GLS", WireframeGlass, "Reference", "Reference::Surfaces - STEP",
                "Reference::Surfaces - STEP::Surfaces - GLS", StepGlass, StepMismatch, "01_CW Panels"
            ],
            Objects = objects
        };
    }

    private static PanelCladdingReportObject Panel(
        Guid id, string name, string layer, string pid, string? cid = null, string unit = "F", string role = "parent",
        string? logic = null, Dictionary<string, string>? extra = null, string[]? remove = null)
    {
        var text = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string key in PanelCladdingPidSetupService.RequiredKeys)
        {
            text[key] = PanelCladdingKeyService.PersistedBlankCellValue;
        }
        text[PanelCladdingSpawnPlanningService.PanelIdUserTextKey] = pid;
        text[PanelCladdingSpawnPlanningService.CidUserTextKey] = cid ?? PanelCladdingCidService.FromPanelId(pid);
        text[PanelCladdingCidService.UnitTypeUserTextKey] = unit;
        text[PanelCladdingCidService.RoleDefinitionKey] = role;
        text[PanelCladdingKeyService.UnitWidthKey] = "90.00000";
        text[PanelCladdingKeyService.UnitHeightKey] = "305.00000";
        text[PanelCladdingKeyService.UnitDimensionKey] = "90.00000x305.00000";
        text["CW_1.05_LOT"] = "01";
        text[PanelCladdingKeyService.PlaneKey] = "{0, 0, 0};{1,0,0};{0,0,1}";
        if (logic is not null)
        {
            text[PanelCladdingKeyService.CladdingLogicKey] = logic;
        }
        foreach ((string key, string value) in extra ?? new())
        {
            text[key] = value;
        }
        foreach (string key in remove ?? [])
        {
            text.Remove(key);
        }
        return new PanelCladdingReportObject(id, name, layer, PanelCladdingReportObjectKind.Brep, text);
    }

    private static PanelCladdingReportObject Dependency(
        Guid id, string layer, string pid, string cid, string cladding = "", bool curve = false)
    {
        var text = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PanelCladdingSpawnPlanningService.PanelIdUserTextKey] = pid,
            [PanelCladdingSpawnPlanningService.CidUserTextKey] = cid
        };
        if (cladding.Length > 0)
        {
            text["Cladding"] = cladding;
        }
        return new PanelCladdingReportObject(id, PanelCladdingCidService.ShortName(cid), layer,
            curve ? PanelCladdingReportObjectKind.Curve : PanelCladdingReportObjectKind.Brep, text);
    }

    private static void Render(FrameworkElement root, int width, int height, string fileName)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0d, 0d, width, height));
        root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string path = Path.Combine(RegressionPaths.Output("pcreport-command"), fileName);
        using (FileStream stream = File.Create(path))
        {
            encoder.Save(stream);
        }
        Require(new FileInfo(path).Length > 15_000L, $"Off-screen report snapshot is unexpectedly empty: {path}");
    }

    private static T Named<T>(Window window, string name) where T : class =>
        window.FindName(name) as T ?? throw new InvalidOperationException($"{name} is missing.");

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class RecordingNavigator : IPanelReportNavigator
    {
        public List<IReadOnlyCollection<Guid>> Calls { get; } = [];

        public void Show(IReadOnlyCollection<Guid> objectIds) => Calls.Add(objectIds.ToArray());
    }
}
