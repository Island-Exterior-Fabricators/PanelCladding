namespace PanelCladdingEditor.Application.Interfaces;

public interface IPanelViewportHighlight : IDisposable
{
    void SetTarget(uint documentRuntimeSerialNumber, Guid objectId);
    void SetVisible(bool visible);
}
