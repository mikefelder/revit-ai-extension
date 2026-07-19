using AgentCore;

namespace RevitAssistant.ViewModels;

/// <summary>
/// MVVM view-model backing <see cref="AssistantPane"/> (spec §5.1). Owns the transcript, drives
/// the <see cref="AgentLoop"/>, marshals streaming/tool events onto the UI thread, and exposes
/// Send/Stop commands. Implements <see cref="IAgentEventSink"/> to receive loop callbacks.
/// </summary>
public sealed class AssistantViewModel : IAgentEventSink
{
    // TODO(spec §5.1, §7 Phase 2): observable message collection, Send/Stop commands,
    // approval-card wiring, cancellation token source.

    public void OnTextDelta(string text) => throw new NotImplementedException();
    public void OnToolStarted(ToolCall call, ToolKind kind) => throw new NotImplementedException();
    public void OnToolFinished(ToolCall call, ToolResult result) => throw new NotImplementedException();
    public void OnCompleted() => throw new NotImplementedException();
    public void OnError(Exception ex) => throw new NotImplementedException();
}
