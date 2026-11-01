using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.UI;

// Modeless PCreport window. Read-only: Rescan reads the model again, selecting issue rows selects
// and zooms their objects in Rhino, Export writes the report to a workbook.
public partial class PanelCladdingReportWindow : Window
{
    private static readonly Brush ErrorBrush = Frozen(Color.FromRgb(0xB2, 0x33, 0x2C));
    private static readonly Brush WarningBrush = Frozen(Color.FromRgb(0xD0, 0x8A, 0x00));
    private static readonly Brush InfoBrush = Frozen(Color.FromRgb(0x8A, 0x96, 0x9C));

    private readonly Func<OperationResponse<PanelCladdingReport>> _scan;
    private readonly IPanelReportNavigator _navigator;
    private readonly Func<string, PanelCladdingReport, OperationResponse> _export;
    private PanelCladdingReport? _report;

    public PanelCladdingReportWindow(
        Func<OperationResponse<PanelCladdingReport>> scan,
        IPanelReportNavigator navigator,
        Func<string, PanelCladdingReport, OperationResponse> export)
    {
        _scan = scan;
        _navigator = navigator;
        _export = export;
        InitializeComponent();
        Closed += (_, _) => ReportClosed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ReportClosed;

    public PanelCladdingReport? Report => _report;

    public OperationResponse Rescan()
    {
        OperationResponse<PanelCladdingReport> scanned = _scan();
        if (!scanned.Success || scanned.Data is null)
        {
            StatusText.Text = scanned.Message;
            return OperationResponse.Fail(scanned.Message);
        }
        ShowReport(scanned.Data);
        return OperationResponse.Ok();
    }

    public void ShowReport(PanelCladdingReport report)
    {
        _report = report;
        DocumentText.Text = System.IO.Path.GetFileNameWithoutExtension(report.DocumentPath);
        ErrorCountText.Text = Plural(report.Count(PanelCladdingReportSeverity.Error), "error", "errors");
        WarningCountText.Text = Plural(report.Count(PanelCladdingReportSeverity.Warning), "warning", "warnings");
        InfoCountText.Text = $"{report.Count(PanelCladdingReportSeverity.Info).ToString("N0", CultureInfo.InvariantCulture)} info";
        StatusText.Text = string.Empty;

        RuleRow[] rules = report.Issues
            .GroupBy(issue => issue.Rule)
            .Select(group => new RuleRow(group.Key, group.ToArray()))
            .ToArray();
        ICollectionView view = CollectionViewSource.GetDefaultView(rules);
        view.GroupDescriptions.Clear();
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(RuleRow.CategoryName)));
        RuleList.ItemsSource = view;
        bool empty = rules.Length == 0;
        EmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        IssueList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        RuleList.SelectedIndex = empty ? -1 : 0;
        if (empty)
        {
            ShowRule(null);
        }
    }

    private void ShowRule(RuleRow? rule)
    {
        RuleTitleText.Text = rule?.Title ?? string.Empty;
        RuleFixText.Text = rule?.Rule.Fix ?? string.Empty;
        IssueList.ItemsSource = rule?.Issues;
    }

    private void OnRuleSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ShowRule(RuleList.SelectedItem as RuleRow);

    private void OnIssueSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Guid[] ids = IssueList.SelectedItems.OfType<IssueRow>()
            .Select(row => row.ObjectId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        if (ids.Length > 0)
        {
            _navigator.Show(ids);
        }
    }

    private void OnRescanClick(object sender, RoutedEventArgs e) => Rescan();

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        if (_report is null)
        {
            return;
        }
        var dialog = new SaveFileDialog
        {
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            FileName = System.IO.Path.GetFileNameWithoutExtension(_report.DocumentPath) + " - PC Report.xlsx",
            InitialDirectory = System.IO.Path.GetDirectoryName(_report.DocumentPath) ?? string.Empty,
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }
        OperationResponse exported = _export(dialog.FileName, _report);
        StatusText.Text = exported.Success
            ? "Exported " + System.IO.Path.GetFileName(dialog.FileName)
            : exported.Message;
    }

    private static string Plural(int count, string one, string many) =>
        $"{count.ToString("N0", CultureInfo.InvariantCulture)} {(count == 1 ? one : many)}";

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public sealed class RuleRow
    {
        public RuleRow(PanelCladdingReportRule rule, IReadOnlyList<PanelCladdingReportIssue> issues)
        {
            Rule = rule;
            Issues = issues.Select(issue => new IssueRow(issue)).ToArray();
        }

        public PanelCladdingReportRule Rule { get; }
        public IReadOnlyList<IssueRow> Issues { get; }
        public string Title => Rule.Title;
        public string CategoryName => Rule.Category.ToString();
        public string CountText => Issues.Count.ToString("N0", CultureInfo.InvariantCulture);
        public Brush SeverityBrush => Rule.Severity switch
        {
            PanelCladdingReportSeverity.Error => ErrorBrush,
            PanelCladdingReportSeverity.Warning => WarningBrush,
            _ => InfoBrush
        };
    }

    public sealed class IssueRow
    {
        public IssueRow(PanelCladdingReportIssue issue)
        {
            ObjectId = issue.ObjectId;
            Object = issue.ObjectName.Length > 0 || issue.ObjectId == Guid.Empty
                ? issue.ObjectName
                : issue.ObjectId.ToString("D")[..8];
            Layer = issue.LayerPath;
            Detail = issue.Detail;
        }

        public Guid ObjectId { get; }
        public string Object { get; }
        public string Layer { get; }
        public string Detail { get; }
    }
}
