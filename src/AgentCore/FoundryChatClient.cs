namespace AgentCore;

/// <summary>
/// Streaming chat-completions client against the Azure AI Foundry model deployment
/// (spec §2.1 D5, §5.2). Uses the OpenAI-compatible endpoint so the deployed model
/// (GPT-4.1 / GPT-5-class / Claude via the Foundry catalog) is swappable without code changes.
/// </summary>
public sealed class FoundryChatClient
{
    private readonly FoundryConfig _config;

    public FoundryChatClient(FoundryConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Stream a chat completion with tool calling. Yields text deltas as they arrive; the final
    /// result carries any requested tool calls. TODO(spec §5.2, Phase 1).
    /// </summary>
    public IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolSchema> tools,
        CancellationToken ct) => throw new NotImplementedException();
}

/// <summary>An incremental event from the streaming model call.</summary>
public abstract record ChatStreamEvent
{
    /// <summary>A streamed text delta to append to the assistant message.</summary>
    public sealed record TextDelta(string Text) : ChatStreamEvent;

    /// <summary>The turn completed; carries any tool calls the model requested.</summary>
    public sealed record Completed(IReadOnlyList<ToolCall> ToolCalls) : ChatStreamEvent;
}
