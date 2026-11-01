extern alias rhinocommon;

using System.Runtime.InteropServices;
using PanelCladdingEditor.Infrastructure.WireframeWeb;
using GetOption = rhinocommon::Rhino.Input.Custom.GetOption;
using GetResult = rhinocommon::Rhino.Input.GetResult;
using Result = rhinocommon::Rhino.Commands.Result;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoCommand = rhinocommon::Rhino.Commands.Command;
using RhinoDialogs = rhinocommon::Rhino.UI.Dialogs;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RunMode = rhinocommon::Rhino.Commands.RunMode;
using ShowMessageButton = rhinocommon::Rhino.UI.ShowMessageButton;
using ShowMessageIcon = rhinocommon::Rhino.UI.ShowMessageIcon;
using ShowMessageResult = rhinocommon::Rhino.UI.ShowMessageResult;

namespace PanelCladdingEditor.UI;

// Opens the active saved model in Wireframe Web when it is installed. Wireframe Web reads the saved
// file, so unsaved changes are saved first or left out. The launcher runs on a worker thread; its
// result is written to the command line when it exits.
[Guid("A073B617-D8ED-4689-8B3E-3B05B6B9F7E8")]
public sealed class PanelCladdingWebCommand : RhinoCommand
{
    private static int _opening;

    public override string EnglishName => "PCweb";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode) =>
        Open(doc, document => mode == RunMode.Interactive ? AskInDialog(document) : AskOnCommandLine());

    // The command once the unsaved-changes question is answered: true saves, false opens the last
    // save, null cancels. Live tests pass the answer directly.
    internal static Result Open(RhinoDoc doc, Func<RhinoDoc, bool?> askToSave)
    {
        if (doc is null || string.IsNullOrWhiteSpace(doc.Path))
        {
            RhinoApp.WriteLine("PCweb requires a saved model named \"<CODE> - Wireframe.3dm\".");
            return Result.Failure;
        }
        string file = Path.GetFileName(doc.Path);
        string code = WireframeWebLauncher.ProjectCode(doc.Path);
        if (code.Length == 0)
        {
            RhinoApp.WriteLine($"PCweb: Wireframe Web needs a model named \"<CODE> - ....3dm\"; {file} has no project code.");
            return Result.Failure;
        }
        string? launcher = WireframeWebLauncher.FindLauncher();
        if (launcher is null)
        {
            RhinoApp.WriteLine("PCweb: " + WireframeWebLauncher.NotInstalledMessage);
            return Result.Failure;
        }
        if (Volatile.Read(ref _opening) == 1)
        {
            RhinoApp.WriteLine("PCweb: Wireframe Web is still opening the previous model.");
            return Result.Nothing;
        }
        if (doc.Modified && doc.IsReadOnly)
        {
            // Rhino cannot save it, so there is nothing to ask.
            RhinoApp.WriteLine($"PCweb: {file} is read-only; Wireframe Web opens its last save.");
        }
        else if (doc.Modified)
        {
            bool? save = askToSave(doc);
            if (save is null) return Result.Cancel;
            if (save == true && !doc.Save())
            {
                RhinoApp.WriteLine($"PCweb: {file} could not be saved.");
                return Result.Failure;
            }
        }

        Interlocked.Exchange(ref _opening, 1);
        string path = doc.Path;
        RhinoApp.WriteLine($"PCweb: opening {code} in Wireframe Web...");
        Task.Run(() =>
        {
            WireframeWebResult result;
            try
            {
                result = WireframeWebLauncher.Open(launcher, path, WireframeWebLauncher.Timeout);
            }
            catch (Exception ex)
            {
                result = WireframeWebResult.Failed(-1, ex.Message);
            }
            string message = result.Success
                ? $"PCweb: {code} is open in Wireframe Web: {result.Address}"
                : result.ExitCode > 0
                    ? $"PCweb: {result.Message} (exit code {result.ExitCode})"
                    : $"PCweb: {result.Message}";
            Interlocked.Exchange(ref _opening, 0);
            RhinoApp.InvokeOnUiThread(new Action(() => RhinoApp.WriteLine(message)));
        });
        return Result.Success;
    }

    private static bool? AskInDialog(RhinoDoc doc) =>
        RhinoDialogs.ShowMessage(
            $"Save changes to {Path.GetFileName(doc.Path)} before opening it in Wireframe Web?",
            "PCweb",
            ShowMessageButton.YesNoCancel,
            ShowMessageIcon.Question) switch
        {
            ShowMessageResult.Yes => true,
            ShowMessageResult.No => false,
            _ => null,
        };

    // Scripted runs (-PCweb): Enter saves, LastSave opens the saved file as it is.
    private static bool? AskOnCommandLine()
    {
        using var getter = new GetOption();
        getter.SetCommandPrompt("Unsaved changes. Save before opening Wireframe Web");
        getter.SetCommandPromptDefault("Save");
        int save = getter.AddOption("Save");
        int lastSave = getter.AddOption("LastSave");
        getter.AcceptNothing(true);
        GetResult result = getter.Get();
        if (result == GetResult.Nothing) return true;
        if (result != GetResult.Option) return null;
        int chosen = getter.OptionIndex();
        return chosen == save ? true : chosen == lastSave ? false : null;
    }
}
