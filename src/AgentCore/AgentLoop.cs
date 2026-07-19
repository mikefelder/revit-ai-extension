namespace AgentCore;

/// <summary>
/// The client-side agent loop (spec §2.1 D1, §5.2): model call -> tool call -> tool result ->
/// model call, running entirely in-process. Foundry supplies only the model endpoint. Reads
/// auto-execute; writes pause for approval via <see cref="IApprovalHandler"/> (D3). Enforces
/// max-iteration and result-truncation guards.
/// </summary>
public sealed class AgentLoop
{
    private readonly FoundryChatClient _foundry;
    private readonly McpToolBroker _broker;
    private readonly IApprovalHandler _approval;
    private readonly IRevitContextProvider _context;
    private readonly Transcript _transcript;

    /// <summary>Hard cap on model/tool round-trips per user turn (spec §5.2).</summary>
    public int MaxIterations { get; init; } = 12;

    public AgentLoop(
        FoundryChatClient foundry,
        McpToolBroker broker,
        IApprovalHandler approval,
        IRevitContextProvider context,
        Transcript transcript)
    {
        _foundry = foundry;
        _broker = broker;
        _approval = approval;
        _context = context;
        _transcript = transcript;
    }

    /// <summary>
    /// Process one user message: inject context, stream the response to <paramref name="sink"/>,
    /// route tool calls (gating writes through approval), and loop until the model stops or a
    /// guard trips. TODO(spec §5.2).
    /// </summary>
    public Task SendAsync(string userText, IAgentEventSink sink, CancellationToken ct) =>
        throw new NotImplementedException();
}

/// <summary>UI-facing callbacks for streaming tokens and tool activity (spec §5.1).</summary>
public interface IAgentEventSink
{
    void OnTextDelta(string text);
    void OnToolStarted(ToolCall call, ToolKind kind);
    void OnToolFinished(ToolCall call, ToolResult result);
    void OnCompleted();
    void OnError(Exception ex);
}
