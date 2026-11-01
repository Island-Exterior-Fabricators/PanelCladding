namespace PanelCladdingEditor.Application.Interfaces;

/// <summary>Shows report issues in the model: selects the objects and zooms to them.</summary>
public interface IPanelReportNavigator
{
    void Show(IReadOnlyCollection<Guid> objectIds);
}
