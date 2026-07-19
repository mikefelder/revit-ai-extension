using AgentCore;

namespace RevitAssistant;

/// <summary>
/// Revit-backed implementation of <see cref="IRevitContextProvider"/> (spec §5.4). Gathers a
/// cheap per-turn snapshot (document, active view, selection, units). All Revit API access runs
/// on a valid API context via ExternalEvent; never blocks the Revit UI thread on network IO.
/// </summary>
public sealed class RevitContextProvider : IRevitContextProvider
{
    public Task<RevitContextSnapshot> SnapshotAsync(CancellationToken ct)
    {
        // TODO(spec §5.4, §8): marshal onto the Revit API context and read document/view/selection.
        throw new NotImplementedException();
    }
}
