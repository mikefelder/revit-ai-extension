using System.Windows.Controls;

namespace RevitAssistant.Controls;

/// <summary>
/// Renders a single tool call as a card: tool name, human-readable argument summary, status
/// (pending / running / done / failed), collapsible raw JSON, and — for write-classified calls —
/// Approve / Reject buttons (spec §5.1). Full template is authored in Phase 2.
/// </summary>
public sealed class ToolPlanCard : UserControl
{
    // TODO(spec §5.1, §7 Phase 2).
}
