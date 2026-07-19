using Autodesk.Revit.UI;

namespace RevitAssistant;

/// <summary>
/// Revit entry point (spec §7 Phase 2). Registers the dockable assistant pane and a ribbon
/// button to toggle it. All Revit API access happens on a valid API context / the UI thread via
/// ExternalEvent (see docs/threading.md — to be authored in Phase 2, spec §7).
/// </summary>
public sealed class App : IExternalApplication
{
    /// <summary>Stable identifier for the dockable pane.</summary>
    public static readonly DockablePaneId PaneId = new(new Guid("D0C4B1E0-0000-0000-0000-000000000001"));

    public Result OnStartup(UIControlledApplication application)
    {
        // TODO(spec §5.1, §7 Phase 2): register DockablePane provider + ribbon button.
        throw new NotImplementedException();
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        // TODO(spec §7 Phase 2): tear down the MCP server child process and pane.
        throw new NotImplementedException();
    }
}
