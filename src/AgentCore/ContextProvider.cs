namespace AgentCore;

/// <summary>
/// Supplies a cheap snapshot of the current Revit context injected into the model each turn
/// (spec §5.4) — the "Copilot reads your active file" equivalent. Implemented by the Revit
/// add-in via the Revit API (on a valid API context); stubbed as "console mode" by the harness.
/// </summary>
public interface IRevitContextProvider
{
    Task<RevitContextSnapshot> SnapshotAsync(CancellationToken ct);
}

/// <summary>Cheap, per-turn context about the open model (spec §5.4).</summary>
public sealed record RevitContextSnapshot(
    string DocumentTitle,
    string? DocumentPath,
    string RevitVersion,
    string ProjectUnits,
    string? ActiveViewName,
    string? ActiveViewType,
    string? ActiveViewLevel,
    int SelectionCount,
    IReadOnlyList<SelectedElement> Selection)
{
    /// <summary>Render as a fenced block for injection into the context message. TODO(spec §5.4).</summary>
    public string ToPromptBlock() => throw new NotImplementedException();
}

/// <summary>A single selected element summary (up to 20 injected per turn, spec §5.4).</summary>
public sealed record SelectedElement(long ElementId, string Category, string Name);
