extern alias rhinocommon;

using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;
using GetResult = rhinocommon::Rhino.Input.GetResult;
using GetString = rhinocommon::Rhino.Input.Custom.GetString;
using Result = rhinocommon::Rhino.Commands.Result;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoCommand = rhinocommon::Rhino.Commands.Command;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RunMode = rhinocommon::Rhino.Commands.RunMode;

namespace PanelCladdingEditor.UI;

// Records the project's PNL code format in "<PROJECT> - Naming Convention.xlsx" and lays out the
// PNL sheet for it. Wall-Type, Frame-Config, Frame-Type and Cladding are asked one at a time;
// Definition is always 01 (01..99, then A1..Z9, then AA..ZZ).
[System.Runtime.InteropServices.Guid("C2F02782-3BB5-4604-8345-88B88C6DB9EE")]
public sealed class PanelCladdingTemplatePnlCommand : RhinoCommand
{
    public override string EnglishName => "PCtemplatePNL";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (string.IsNullOrWhiteSpace(doc.Path))
        {
            RhinoApp.WriteLine("Save the Rhino document before running PCtemplatePNL.");
            return Result.Failure;
        }
        string project = PanelCladdingProjectService.CodeFromDocumentPath(doc.Path);
        string? path = project.Length == 0 ? null : LivePanelNamingConventionSync.ResolvePath(doc, null, project);
        if (path is null || !File.Exists(path))
        {
            RhinoApp.WriteLine($"PCtemplatePNL needs the naming convention sheet {path ?? "<PROJECT>" + LivePanelNamingConventionSync.FileNameSuffix}.");
            return Result.Failure;
        }
        string file = Path.GetFileName(path);
        var template = new OpenXmlPanelNamingTemplate();
        var state = template.Inspect(path);
        if (!state.Success || state.Data is null)
        {
            RhinoApp.WriteLine(state.Message);
            return Result.Failure;
        }
        PanelNamingFormat? recorded = state.Data.RecordedFormat.Length == 0 ? null : PanelNamingFormat.Parse(state.Data.RecordedFormat).Data;
        RhinoApp.WriteLine(recorded is null
            ? $"{file} has no PNL format yet; the sheet sync uses {PanelNamingFormat.Legacy.Serialize()}."
            : $"{file} PNL format: {recorded.Serialize()}.");
        RhinoApp.WriteLine("Formats: AA = two letters, A = one letter, A1 = letter + digit, 01 = two digits, 001 = three digits. Letters I and O are not used.");

        var answers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string title in PanelNamingFormat.SegmentTitles.Take(4))
        {
            while (!answers.ContainsKey(title))
            {
                using var input = new GetString();
                input.SetCommandPrompt($"{title} code format (AA, A, A1, 01, 001)");
                input.AcceptNothing(true);
                GetResult entered = input.Get();
                if (entered == GetResult.Nothing)
                {
                    RhinoApp.WriteLine($"An answer is required for {title}.");
                    continue;
                }
                if (entered != GetResult.String) return input.CommandResult();
                string? token = PanelNamingFormat.ReadToken(input.StringResult());
                if (token is null)
                {
                    RhinoApp.WriteLine($"'{input.StringResult().Trim()}' is not a format; answer AA, A, A1, 01 or 001.");
                    continue;
                }
                IReadOnlyList<string> misfits = OpenXmlPanelNamingTemplate.Misfits(state.Data, title, token);
                if (misfits.Count > 0)
                {
                    RhinoApp.WriteLine($"{title} codes {string.Join(", ", misfits)} already have values and do not fit {token}; " +
                        "codes never move, so choose a format they fit.");
                    continue;
                }
                answers[title] = token;
            }
        }
        var format = new PanelNamingFormat(answers["Wall-Type"], answers["Frame-Config"], answers["Frame-Type"], answers["Cladding"],
            PanelNamingFormat.DefinitionToken);
        RhinoApp.WriteLine($"Definition is {PanelNamingFormat.DefinitionToken}: 01..99, then A1..Z9, then AA..ZZ.");

        var applied = template.Apply(path, format);
        if (!applied.Success || applied.Data is null)
        {
            RhinoApp.WriteLine(applied.Message);
            return Result.Failure;
        }
        foreach (string note in applied.Data.Notes) RhinoApp.WriteLine("PCtemplatePNL: " + note);
        // Every open model reads the new format again; this one also keeps a copy of it.
        LivePanelNamingFormat.ForgetAll();
        LivePanelFrameConfigCodes.ForgetAll();
        LivePanelNamingFormat.Mirror(doc);
        RhinoApp.WriteLine(applied.Data.Changed
            ? $"PCtemplatePNL: {file} now uses {format.Serialize()}."
            : $"PCtemplatePNL: {file} already uses {format.Serialize()}; nothing changed.");
        return Result.Success;
    }
}
