using System.Windows.Controls;
using Autodesk.Revit.UI;

namespace RevitAssistant;

/// <summary>
/// Code-behind for the dockable assistant pane (spec §5.1). Implements
/// <see cref="IDockablePaneProvider"/> so Revit can host this control as a dockable pane.
/// </summary>
public partial class AssistantPane : UserControl, IDockablePaneProvider
{
    public AssistantPane()
    {
        InitializeComponent();
    }

    public void SetupDockablePane(DockablePaneProviderData data)
    {
        // TODO(spec §5.1): data.FrameworkElement = this; set default docking (right) + state.
        throw new NotImplementedException();
    }
}
