using System.Text;

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
    /// <summary>Render as a fenced block for injection into the context message (spec §5.4).</summary>
    public string ToPromptBlock()
    {
        var sb = new StringBuilder();
        sb.AppendLine("```revit-context");
        sb.AppendLine($"document: {DocumentTitle}");
        if (!string.IsNullOrEmpty(DocumentPath))
            sb.AppendLine($"path: {DocumentPath}");
        sb.AppendLine($"revitVersion: {RevitVersion}");
        sb.AppendLine($"units: {ProjectUnits}");
        sb.AppendLine($"activeView: {ActiveViewName ?? "(none)"}"
            + (ActiveViewType is null ? "" : $" [{ActiveViewType}]")
            + (ActiveViewLevel is null ? "" : $" @ {ActiveViewLevel}"));
        sb.AppendLine($"selectionCount: {SelectionCount}");
        if (Selection.Count > 0)
        {
            sb.AppendLine("selection:");
            foreach (var e in Selection)
                sb.AppendLine($"  - [{e.ElementId}] {e.Category}: {e.Name}");
        }
        sb.Append("```");
        return sb.ToString();
    }
}

/// <summary>A single selected element summary (up to 20 injected per turn, spec §5.4).</summary>
public sealed record SelectedElement(long ElementId, string Category, string Name);
