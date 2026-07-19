using System.Runtime.CompilerServices;
using Xunit;

namespace AgentCore.Tests;

/// <summary>
/// Agent-loop behavior with a mock model and mock broker (spec §5.2, §7): read auto-execution,
/// write approval gating (approve/reject), and the max-iteration guard.
/// </summary>
public class AgentLoopTests
{
    [Fact]
    public async Task WriteRejected_DoesNotCallTool_AndReturnsStructuredRefusal()
    {
        var toolCall = new ToolCall("call_1", "set_parameter", "{}");
        var foundry = new FakeFoundry(
            [new ChatStreamEvent.Completed([toolCall])],
            [new ChatStreamEvent.Completed([])]);
        var broker = new FakeBroker { Kind = ToolKind.Write };
        var approval = new FakeApproval { Approve = false };
        var transcript = new Transcript();
        var loop = NewLoop(foundry, broker, approval, transcript);

        await loop.SendAsync("rename stuff", new FakeSink(), CancellationToken.None);

        Assert.Equal(1, approval.Requests);
        Assert.Equal(0, broker.CallToolInvocations);
        var toolMsg = Assert.Single(transcript.Messages, m => m.Role == ChatRole.Tool);
        Assert.Contains("rejected_by_user", toolMsg.Content);
    }

    [Fact]
    public async Task WriteApproved_CallsTool()
    {
        var toolCall = new ToolCall("call_1", "set_parameter", "{}");
        var foundry = new FakeFoundry(
            [new ChatStreamEvent.Completed([toolCall])],
            [new ChatStreamEvent.Completed([])]);
        var broker = new FakeBroker { Kind = ToolKind.Write };
        var approval = new FakeApproval { Approve = true };
        var transcript = new Transcript();
        var loop = NewLoop(foundry, broker, approval, transcript);

        await loop.SendAsync("rename stuff", new FakeSink(), CancellationToken.None);

        Assert.Equal(1, approval.Requests);
        Assert.Equal(1, broker.CallToolInvocations);
    }

    [Fact]
    public async Task Reads_AutoExecute_WithoutApproval()
    {
        var toolCall = new ToolCall("call_1", "get_walls", "{}");
        var foundry = new FakeFoundry(
            [new ChatStreamEvent.Completed([toolCall])],
            [new ChatStreamEvent.Completed([])]);
        var broker = new FakeBroker { Kind = ToolKind.Read };
        var approval = new FakeApproval { Approve = false };
        var transcript = new Transcript();
        var loop = NewLoop(foundry, broker, approval, transcript);

        await loop.SendAsync("list walls", new FakeSink(), CancellationToken.None);

        Assert.Equal(0, approval.Requests);
        Assert.Equal(1, broker.CallToolInvocations);
    }

    [Fact]
    public async Task MaxIterations_Guard_StopsLoop()
    {
        // Every turn requests a (read) tool call, so the loop would run forever without the guard.
        var foundry = new FakeFoundry([new ChatStreamEvent.Completed([new ToolCall("c", "get_walls", "{}")])]);
        var broker = new FakeBroker { Kind = ToolKind.Read };
        var sink = new FakeSink();
        var loop = new AgentLoop(foundry, broker, new FakeApproval(), new FakeContext(), new Transcript())
        {
            MaxIterations = 3,
        };

        await loop.SendAsync("loop", sink, CancellationToken.None);

        Assert.Equal(3, broker.CallToolInvocations);
        Assert.True(sink.Completed);
        Assert.Contains("maximum number", sink.Text);
    }

    private static AgentLoop NewLoop(
        IFoundryChatClient foundry, IToolBroker broker, IApprovalHandler approval, Transcript transcript) =>
        new(foundry, broker, approval, new FakeContext(), transcript);

    private sealed class FakeFoundry(params IReadOnlyList<ChatStreamEvent>[] turns) : IFoundryChatClient
    {
        private readonly Queue<IReadOnlyList<ChatStreamEvent>> _turns = new(turns);
        private IReadOnlyList<ChatStreamEvent> _last = [new ChatStreamEvent.Completed([])];

        public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolSchema> tools,
            [EnumeratorCancellation] CancellationToken ct)
        {
            if (_turns.Count > 0)
                _last = _turns.Dequeue();

            foreach (var evt in _last)
            {
                await Task.Yield();
                yield return evt;
            }
        }
    }

    private sealed class FakeBroker : IToolBroker
    {
        public ToolKind Kind { get; init; } = ToolKind.Read;
        public int CallToolInvocations { get; private set; }

        public Task<IReadOnlyList<ToolSchema>> ListEnabledToolsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ToolSchema>>([]);

        public ToolKind Classify(string toolName) => Kind;
        public bool IsWrite(string toolName) => Kind == ToolKind.Write;

        public Task<ToolResult> CallToolAsync(ToolCall call, CancellationToken ct)
        {
            CallToolInvocations++;
            return Task.FromResult(new ToolResult("ok", Truncated: false));
        }
    }

    private sealed class FakeApproval : IApprovalHandler
    {
        public bool Approve { get; init; }
        public int Requests { get; private set; }

        public Task<ApprovalVerdict> RequestAsync(ApprovalRequest request, CancellationToken ct)
        {
            Requests++;
            return Task.FromResult(new ApprovalVerdict(Approve, Approve ? null : "rejected"));
        }
    }

    private sealed class FakeContext : IRevitContextProvider
    {
        public Task<RevitContextSnapshot> SnapshotAsync(CancellationToken ct) =>
            Task.FromResult(new RevitContextSnapshot(
                "Doc", null, "2026", "mm", "View", "FloorPlan", "L1", 0, []));
    }

    private sealed class FakeSink : IAgentEventSink
    {
        private readonly System.Text.StringBuilder _text = new();
        public string Text => _text.ToString();
        public bool Completed { get; private set; }
        public Exception? Error { get; private set; }

        public void OnTextDelta(string text) => _text.Append(text);
        public void OnToolStarted(ToolCall call, ToolKind kind) { }
        public void OnToolFinished(ToolCall call, ToolResult result) { }
        public void OnCompleted() => Completed = true;
        public void OnError(Exception ex) => Error = ex;
    }
}
