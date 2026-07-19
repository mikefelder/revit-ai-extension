using System.Text;
using System.Text.Json;

namespace AgentCore;

/// <summary>
/// The client-side agent loop (spec §2.1 D1, §5.2): model call -> tool call -> tool result ->
/// model call, running entirely in-process. Foundry supplies only the model endpoint. Reads
/// auto-execute; writes pause for approval via <see cref="IApprovalHandler"/> (D3). Enforces
/// max-iteration and result-truncation guards.
/// </summary>
public sealed class AgentLoop
{
    private readonly IFoundryChatClient _foundry;
    private readonly IToolBroker _broker;
    private readonly IApprovalHandler _approval;
    private readonly IRevitContextProvider _context;
    private readonly Transcript _transcript;

    /// <summary>Hard cap on model/tool round-trips per user turn (spec §5.2).</summary>
    public int MaxIterations { get; init; } = 12;

    /// <summary>Base system prompt (spec §5.5); overridable from settings.</summary>
    public string SystemPrompt { get; init; } = DefaultSystemPrompt;

    public AgentLoop(
        IFoundryChatClient foundry,
        IToolBroker broker,
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
    /// guard trips (spec §5.2).
    /// </summary>
    public async Task SendAsync(string userText, IAgentEventSink sink, CancellationToken ct)
    {
        try
        {
            var context = await _context.SnapshotAsync(ct);
            var contextBlock = context.ToPromptBlock();

            if (_transcript.Messages.Count == 0)
                _transcript.Add(new ChatMessage(ChatRole.System, $"{SystemPrompt}\n\n{contextBlock}"));
            else
                _transcript.Add(new ChatMessage(ChatRole.System, $"Updated Revit context:\n{contextBlock}"));

            _transcript.Add(new ChatMessage(ChatRole.User, userText));

            var tools = await _broker.ListEnabledToolsAsync(ct);

            for (var iteration = 0; iteration < MaxIterations; iteration++)
            {
                ct.ThrowIfCancellationRequested();

                var assistantText = new StringBuilder();
                IReadOnlyList<ToolCall> toolCalls = [];

                await foreach (var evt in _foundry.StreamAsync(_transcript.Messages, tools, ct))
                {
                    switch (evt)
                    {
                        case ChatStreamEvent.TextDelta delta:
                            assistantText.Append(delta.Text);
                            sink.OnTextDelta(delta.Text);
                            break;
                        case ChatStreamEvent.Completed completed:
                            toolCalls = completed.ToolCalls;
                            break;
                    }
                }

                _transcript.Add(new ChatMessage(
                    ChatRole.Assistant,
                    assistantText.ToString(),
                    toolCalls.Count > 0 ? toolCalls : null));

                if (toolCalls.Count == 0)
                {
                    sink.OnCompleted();
                    return;
                }

                foreach (var call in toolCalls)
                {
                    ct.ThrowIfCancellationRequested();

                    var kind = _broker.Classify(call.Name);
                    sink.OnToolStarted(call, kind);

                    ToolResult result;
                    if (kind == ToolKind.Write)
                    {
                        var verdict = await _approval.RequestAsync(
                            new ApprovalRequest(call.Name, call.ArgumentsJson, SummarizeCall(call)), ct);

                        result = verdict.Approved
                            ? await _broker.CallToolAsync(call, ct)
                            : new ToolResult(BuildRejection(verdict.Reason), Truncated: false, RejectedByUser: true);
                    }
                    else
                    {
                        result = await _broker.CallToolAsync(call, ct);
                    }

                    sink.OnToolFinished(call, result);
                    _transcript.Add(new ChatMessage(ChatRole.Tool, result.Content, ToolCallId: call.Id));
                }
            }

            var guard = "Reached the maximum number of tool iterations for this turn; stopping. "
                + "Ask a narrower question or continue.";
            sink.OnTextDelta(guard);
            _transcript.Add(new ChatMessage(ChatRole.Assistant, guard));
            sink.OnCompleted();
        }
        catch (OperationCanceledException)
        {
            sink.OnCompleted();
        }
        catch (Exception ex)
        {
            sink.OnError(ex);
        }
    }

    private static string SummarizeCall(ToolCall call) => $"{call.Name}({call.ArgumentsJson})";

    private static string BuildRejection(string? reason) => JsonSerializer.Serialize(new
    {
        rejected_by_user = true,
        reason = reason ?? "User rejected this write.",
    });

    private const string DefaultSystemPrompt =
        "You are an assistant embedded in Autodesk Revit, operating on the currently open model "
        + "via tools. Prefer tool calls over recall; never invent element data. If a needed tool "
        + "does not exist, say so. Before any write tool call, state in one sentence what will "
        + "change and why, and expect that the user may reject it; never chain multiple write "
        + "calls without intermediate results. Be concise: use tables for element listings and "
        + "state totals explicitly.";
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
