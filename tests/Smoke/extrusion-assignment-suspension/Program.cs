using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using PanelCladdingEditor.UI;

namespace ExtrusionAssignmentSuspensionSmoke;

internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string FrameProfile = "1D-T000";
    private const string SegmentProfile = "1D-T001";

    [STAThread]
    private static void Main()
    {
        FieldInfo enabled = typeof(PanelCladdingEditorWindow).GetField(
                "_extrusionAssignmentEnabled", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Assignment switch is missing.");
        Require(enabled.GetValue(null) is false, "Curve extrusion assignment is not suspended by default.");

        var repository = new Repository(BuildLayout());
        PanelCladdingEditorWindow window = Open(repository);
        FrameworkElement root = (FrameworkElement)window.Content;
        var canvas = Named<PanelCladdingGridCanvas>(window, "PanelGridCanvas");
        Grid floating = Named<Grid>(window, "ExtrusionAssignmentSection");

        Invoke(window, "SwitchView", PanelEditorView.Extrusion, false);
        Require(Named<Border>(window, "ExtrusionLegendSection").Visibility == Visibility.Collapsed,
            "Extrusion view still shows the profile catalogue.");
        Require(Named<Border>(window, "FrameConfigurationSection").Visibility == Visibility.Visible &&
                Named<StackPanel>(window, "ExtrusionActions").Visibility == Visibility.Visible,
            "Extrusion view lost its layout tools.");
        Require(Named<TextBlock>(window, "FrameConfigurationNote").Visibility == Visibility.Collapsed &&
                Named<TextBlock>(window, "FrameConfigurationText").ToolTip is null,
            "Frame configuration still refers to assigned profiles.");

        Select(canvas, "FRM-0");
        Require(floating.Visibility == Visibility.Collapsed, "Frame curve selection revealed assignment.");
        Require(CanvasAssignments(canvas).Count == 0, "Canvas labels still show assigned profiles.");
        PanelExtrusionSegment[] horizontal = Segments(window)
            .Where(segment => !segment.IsFrame && segment.Axis == PanelExtrusionAxis.Horizontal)
            .ToArray();
        Require(horizontal.Length == 2, "Fixture should have two horizontal intermediate segments.");
        Select(canvas, horizontal.Select(segment => segment.Id).ToArray());
        Require(floating.Visibility == Visibility.Collapsed, "Intermediate curve selection revealed assignment.");
        Require(Named<Button>(window, "MergeExtrusionsButton").IsEnabled, "Merge is unavailable for an adjacent run.");
        Render(root, 1440, 900, "extrusion-view-layout-only-1440x900.png");

        Invoke(window, "OnMergeExtrusionsClick", window, new RoutedEventArgs());
        Require(Field<System.Collections.ICollection>(window, "_mergedGroups").Count == 1, "Merge failed.");
        Require(floating.Visibility == Visibility.Collapsed, "Merged selection revealed assignment.");
        Invoke(window, "UndoLastChange");
        Require(Field<System.Collections.ICollection>(window, "_mergedGroups").Count == 0, "Undo merge failed.");
        Select(canvas, horizontal.Select(segment => segment.Id).ToArray());
        Invoke(window, "OnMergeExtrusionsClick", window, new RoutedEventArgs());

        PanelExtrusionSegment vertical = Segments(window)
            .First(segment => !segment.IsFrame && segment.Axis == PanelExtrusionAxis.Vertical);
        Select(canvas, vertical.Id);
        Invoke(window, "OnHideExtrusionsClick", window, new RoutedEventArgs());
        Require(Field<HashSet<string>>(window, "_hiddenExtrusions").Count > 0, "Hide failed.");
        Render(root, 1440, 900, "extrusion-view-merged-hidden-1440x900.png");

        Invoke(window, "OnSaveExtrusionsClick", window, new RoutedEventArgs());
        PanelAttributeCommitRequest commit = repository.LastCommit
            ?? throw new InvalidOperationException("Save Extrusions did not commit.");
        Require(commit.UserTextWrites.TryGetValue(PanelCladdingKeyService.FrameAssignmentsKey, out string? payload) &&
                payload.Contains(FrameProfile, StringComparison.Ordinal) &&
                payload.Contains(SegmentProfile, StringComparison.Ordinal),
            "Save Extrusions dropped the panel's existing assignments.");
        PanelCladdingTopologyState topology = new PanelCladdingKeyService()
            .DecodeTopology(commit.UserTextWrites, 1, 1).Data
            ?? throw new InvalidOperationException("Saved topology does not decode.");
        Require(topology.MergeRuns.Count == 1 && topology.HiddenSegments.Count == 1,
            "Save Extrusions did not record the edited layout.");

        Invoke(window, "SwitchView", PanelEditorView.Cladding, false);
        Require(Named<Border>(window, "MaterialLegendSection").Visibility == Visibility.Visible &&
                Named<Border>(window, "CladdingAssignmentSection").Visibility == Visibility.Visible,
            "Cladding view changed.");
        window.Close();

        enabled.SetValue(null, true);
        try
        {
            PanelCladdingEditorWindow restored = Open(new Repository(BuildLayout()));
            Invoke(restored, "SwitchView", PanelEditorView.Extrusion, false);
            Select(Named<PanelCladdingGridCanvas>(restored, "PanelGridCanvas"), "FRM-0");
            Require(Named<Border>(restored, "ExtrusionLegendSection").Visibility == Visibility.Visible &&
                    Named<Grid>(restored, "ExtrusionAssignmentSection").Visibility == Visibility.Visible &&
                    Named<TextBlock>(restored, "FrameConfigurationNote").Visibility == Visibility.Visible,
                "Turning the switch back on does not restore assignment.");
            Require(CanvasAssignments(Named<PanelCladdingGridCanvas>(restored, "PanelGridCanvas"))
                    .TryGetValue("FRM-0", out IReadOnlyList<string>? codes) && codes.Contains(FrameProfile),
                "Restored canvas labels do not show assigned profiles.");
            restored.Close();
        }
        finally
        {
            enabled.SetValue(null, false);
        }

        Console.WriteLine("PASS: assignment catalogue, floating panel, canvas codes and profile note are hidden by default; " +
            "merge/Undo/hide and Save Extrusions work and keep existing assignments; Cladding view unchanged; switch restores assignment.");
    }

    private static PanelCladdingEditorWindow Open(Repository repository)
    {
        var keys = new PanelCladdingKeyService();
        var signature = new PanelCladdingTypeSignatureService(keys);
        var window = new PanelCladdingEditorWindow(new PanelCladdingEditorController(
            repository, new PanelCladdingSaveService(repository, signature), new PanelPreviewRenderer(), signature))
        {
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None
        };
        OperationResponse loaded = window.LoadPanel(repository.Layout.DocumentPath, repository.Layout.ObjectId);
        Require(loaded.Success, $"Fixture load failed: {loaded.Message}");
        Layout((FrameworkElement)window.Content, 1440, 900);
        return window;
    }

    private static PanelCladdingLayout BuildLayout() => new()
    {
        ObjectId = Guid.Parse("A0431000-0000-0000-0000-000000000031"),
        DocumentPath = "C:/fixtures/extrusion-assignment-suspension.3dm", DocumentRuntimeSerialNumber = 31,
        ObjectName = "N1-04-31", SystemCode = "WT04", GeometryFingerprint = "extrusion-assignment-suspension",
        GeometryClass = PanelGeometryClass.Planar, Width = 2000, Height = 1600,
        ModelTolerance = 0.01, ModelUnitScaleToMillimeters = 1,
        HorizontalOffsets = [800], VerticalOffsets = [1000],
        Cells = Enumerable.Range(0, 2).SelectMany(column => Enumerable.Range(0, 2).Select(row => new PanelCladdingCell
        {
            Column = column, Row = row, RowLabel = PanelCladdingKeyService.GetRowLabel(row),
            ShortLabel = $"{column}{PanelCladdingKeyService.GetRowLabel(row)}",
            UserTextKey = PanelCladdingKeyService.GetCellKey(column, PanelCladdingKeyService.GetRowLabel(row)),
            Value = "MPL-001"
        })).ToArray(),
        FrameAssignments = new PanelFrameAssignmentState
        {
            FrameAssignments = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["FRM-0"] = [FrameProfile]
            },
            SegmentAssignments =
            [
                new PanelFrameSegmentAssignment(
                    new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 0, 0), [SegmentProfile])
            ],
            Definitions = new[] { FrameProfile, SegmentProfile }.ToDictionary(
                code => code,
                code => new PanelFrameProfileDefinition
                {
                    Code = code, BaseCode = code[3..], SourceCode = $"ALU-{code[3..]}", Category = "FRAMING",
                    Dimension = PanelFrameProfileDimension.OneDimensional,
                    Calculation = PanelFrameProfileCalculation.Length, CalculationValue = 1d
                },
                StringComparer.OrdinalIgnoreCase)
        }
    };

    private static IReadOnlyList<PanelExtrusionSegment> Segments(PanelCladdingEditorWindow window) =>
        (IReadOnlyList<PanelExtrusionSegment>)Invoke(window, "CurrentSegments")!;

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> CanvasAssignments(PanelCladdingGridCanvas canvas) =>
        Field<IReadOnlyDictionary<string, IReadOnlyList<string>>>(canvas, "_extrusionAssignments");

    private static void Select(PanelCladdingGridCanvas canvas, params string[] ids) =>
        Invoke(canvas, "ApplySelection", ids, System.Windows.Input.ModifierKeys.None);

    private static T Named<T>(FrameworkElement root, string name) where T : class =>
        root.FindName(name) as T ?? throw new InvalidOperationException($"Missing {name}.");

    private static T Field<T>(object target, string name) =>
        (T)(target.GetType().GetField(name, Private)?.GetValue(target)
            ?? throw new InvalidOperationException($"Missing field {name}."));

    private static object? Invoke(object target, string name, params object?[] args) =>
        target.GetType().GetMethods(Private)
            .Single(method => method.Name == name && method.GetParameters().Length == args.Length)
            .Invoke(target, args);

    private static void Layout(FrameworkElement root, int width, int height)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
    }

    private static void Render(FrameworkElement root, int width, int height, string name)
    {
        Layout(root, width, height);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream file = File.Create(Path.Combine(RegressionPaths.Output("extrusion-assignment-suspension"), name));
        encoder.Save(file);
    }

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }

    private sealed class Repository(PanelCladdingLayout layout) : ILivePanelCladdingRepository
    {
        public PanelCladdingLayout Layout { get; } = layout;
        public PanelAttributeCommitRequest? LastCommit { get; private set; }
        public OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingLayout>.Ok(Layout);
        public OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail("Not needed.");
        public OperationResponse<PanelAttributeCommitResult> CommitAttributes(
            PanelAttributeCommitRequest request,
            Func<OperationResponse> finalizeExternalCommit)
        {
            LastCommit = request;
            OperationResponse finalized = finalizeExternalCommit();
            return finalized.Success
                ? OperationResponse<PanelAttributeCommitResult>.Ok(new PanelAttributeCommitResult
                {
                    ObjectId = request.ObjectId,
                    Mutated = true
                })
                : OperationResponse<PanelAttributeCommitResult>.Fail(finalized.Message);
        }
        public OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath) =>
            OperationResponse<string>.Ok(workbookPath);
    }
}
