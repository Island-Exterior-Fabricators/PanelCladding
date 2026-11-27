namespace PanelCladdingEditor.Application.Interfaces;

public interface IPanelReportNavigator
{
    void Show(IReadOnlyCollection<Guid> objectIds);
}
