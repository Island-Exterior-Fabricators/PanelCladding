extern alias rhinocommon;

using System.Runtime.InteropServices;
using PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;
using LoadReturnCode = rhinocommon::Rhino.PlugIns.LoadReturnCode;
using PlugIn = rhinocommon::Rhino.PlugIns.PlugIn;
using PlugInLoadTime = rhinocommon::Rhino.PlugIns.PlugInLoadTime;

namespace PanelCladdingEditor;

[Guid("7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35")]
public sealed class PanelCladdingEditorPlugin : PlugIn
{
    public static PanelCladdingEditorPlugin? Instance { get; private set; }

    public PanelCladdingEditorPlugin()
    {
        Instance = this;
    }

    public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
        LivePanelNamingConventionSync.Register();
        return LoadReturnCode.Success;
    }
}
