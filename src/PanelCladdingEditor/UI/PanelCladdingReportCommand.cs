extern alias rhinocommon;

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using PanelCladdingEditor.Infrastructure.Rhino;
using PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;
using Result = rhinocommon::Rhino.Commands.Result;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoCommand = rhinocommon::Rhino.Commands.Command;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RunMode = rhinocommon::Rhino.Commands.RunMode;

namespace PanelCladdingEditor.UI;

// Checks layer structure and attributes of the active model and shows the issues. Read-only.
[Guid("2CB10B8E-7C31-478C-8D0A-BE6015F29DF7")]
public sealed class PanelCladdingReportCommand : RhinoCommand
{
    private static PanelCladdingReportWindow? _window;
    private static uint _windowDocument;

    public override string EnglishName => "PCreport";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (doc is null || string.IsNullOrWhiteSpace(doc.Path))
        {
            RhinoApp.WriteLine("PCreport requires a saved active document.");
            return Result.Failure;
        }

        uint documentSerial = doc.RuntimeSerialNumber;
        OperationResponse<PanelCladdingReport> Scan()
        {
            OperationResponse<PanelCladdingReportSnapshot> snapshot =
                LivePanelCladdingReportReader.Read(RhinoDoc.FromRuntimeSerialNumber(documentSerial));
            return snapshot.Success && snapshot.Data is not null
                ? OperationResponse<PanelCladdingReport>.Ok(
                    new PanelCladdingComplianceService().Check(snapshot.Data, DateTime.Now))
                : OperationResponse<PanelCladdingReport>.Fail(snapshot.Message);
        }

        OperationResponse<PanelCladdingReport> report = Scan();
        if (!report.Success || report.Data is null)
        {
            RhinoApp.WriteLine(report.Message);
            return Result.Failure;
        }

        if (_window is not null && _windowDocument != documentSerial)
        {
            _window.Close();
        }
        if (_window is null)
        {
            _window = new PanelCladdingReportWindow(
                Scan,
                new LivePanelReportNavigator(documentSerial),
                OpenXmlPanelCladdingReportWriter.Write);
            _windowDocument = documentSerial;
            _window.ReportClosed += (_, _) => _window = null;
            new WindowInteropHelper(_window) { Owner = RhinoApp.MainWindowHandle() };
        }
        _window.ShowReport(report.Data);
        if (!_window.IsVisible)
        {
            _window.Show();
        }
        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }
        _window.Activate();

        PanelCladdingReport data = report.Data;
        int errors = data.Count(PanelCladdingReportSeverity.Error);
        int warnings = data.Count(PanelCladdingReportSeverity.Warning);
        int info = data.Count(PanelCladdingReportSeverity.Info);
        RhinoApp.WriteLine(
            $"PCreport: {errors} error(s), {warnings} warning(s), {info} info on {data.PanelCount} panel(s), " +
            $"{data.SurfaceCount} surface(s) and {data.CurveCount} curve(s).");
        return Result.Success;
    }
}
